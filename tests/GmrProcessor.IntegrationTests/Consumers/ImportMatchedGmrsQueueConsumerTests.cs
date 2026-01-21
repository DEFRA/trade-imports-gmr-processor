using System.Text.Json;
using Amazon.SQS.Model;
using AutoFixture;
using Azure.Messaging.ServiceBus;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using Defra.TradeImportsGmrFinder.Domain.Events;
using GmrProcessor.Config;
using GmrProcessor.IntegrationTests.Clients;
using GmrProcessor.Processors.ImportGmrMatching;
using TestFixtures;

namespace GmrProcessor.IntegrationTests.Consumers;

[Collection("IntegrationTest")]
public class ImportMatchedGmrsQueueConsumerTests(ServiceBusFixture serviceBusFixture)
    : IntegrationTestBase,
        IClassFixture<ServiceBusFixture>
{
    [Fact]
    public async Task WhenMatchedGmrReceived_MatchIsCreatedFromImports()
    {
        var serviceBusOptions = GetConfig<TradeImportsServiceBusOptions>();
        var serviceBusClient = serviceBusFixture.GetClient(serviceBusOptions.ImportMatchResultQueueName);
        await serviceBusClient.PurgeAsync(TestContext.Current.CancellationToken);

        var expectedMrn = CustomsDeclarationFixtures.GenerateMrn();
        var expectedChedReference = ImportPreNotificationFixtures.GenerateRandomReference();

        var importEvent = await SendImportPreNotificationAsync(expectedChedReference, expectedMrn);
        var expectedImport = importEvent.Resource!;

        var matchedGmrsConfig = GetConfig<ImportMatchedGmrsQueueOptions>();
        var (matchedGmrsClient, matchedGmrsQueueUrl) = await GetSqsClient(matchedGmrsConfig.QueueName);

        var matchedGmrEvent = new MatchedGmr { Mrn = expectedMrn, Gmr = GmrFixtures.GmrFixture().Create() };
        var message = new SendMessageRequest
        {
            MessageBody = JsonSerializer.Serialize(matchedGmrEvent),
            QueueUrl = matchedGmrsQueueUrl,
        };

        await matchedGmrsClient.SendMessageAsync(message, TestContext.Current.CancellationToken);
        await WaitForMessageConsumed(matchedGmrsClient, matchedGmrsQueueUrl);

        // Then : match is written to DB
        var item = await AsyncWaiter.WaitForAsync(
            async () =>
            {
                return await Mongo.MatchedImportNotifications.FindOne(
                    p => p.Mrn == expectedMrn && p.Id == expectedImport.ReferenceNumber,
                    TestContext.Current.CancellationToken
                );
            },
            TestContext.Current.CancellationToken
        );

        item.Should().NotBeNull();
        item.Id.Should().BeEquivalentTo(expectedImport.ReferenceNumber);
        item.Mrn.Should().BeEquivalentTo(matchedGmrEvent.Mrn);

        // Then : message is sent to ASB
        var receiver = serviceBusClient.Receiver;
        var serviceBusReceivedMessage = new List<ServiceBusReceivedMessage>();
        var allMessages = await AsyncWaiter.WaitForAsync(
            async () =>
            {
                var receivedMessages = await receiver.ReceiveMessagesAsync(
                    10,
                    TimeSpan.FromSeconds(1),
                    TestContext.Current.CancellationToken
                );
                serviceBusReceivedMessage.AddRange(receivedMessages);
                return serviceBusReceivedMessage.Count >= 1 ? serviceBusReceivedMessage : null;
            },
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(allMessages);
        allMessages.Count.Should().Be(1, "Too many messages received");

        var importMatchMessage = allMessages[0].Body.ToObjectFromJson<ImportMatchMessage>();

        Assert.NotNull(importMatchMessage);
        importMatchMessage.ImportReference.Should().Be(expectedImport.ReferenceNumber);
        importMatchMessage.Match.Should().BeTrue();
    }

    [Fact]
    public async Task WhenMatchedGmrReceived_MatchIsCreatedFromTransits()
    {
        var serviceBusOptions = GetConfig<TradeImportsServiceBusOptions>();
        var serviceBusClient = serviceBusFixture.GetClient(serviceBusOptions.ImportMatchResultQueueName);
        await serviceBusClient.PurgeAsync(TestContext.Current.CancellationToken);

        var expectedMrn = CustomsDeclarationFixtures.GenerateMrn();
        var expectedTransitReference = ImportPreNotificationFixtures.GenerateRandomReference();

        var dataEventsQueueConfig = GetConfig<GtoDataEventsQueueConsumerOptions>();
        var (sqsClient, queueUrl) = await GetSqsClient(dataEventsQueueConfig.QueueName);

        var transitImportPreNotification = ImportPreNotificationFixtures
            .ImportPreNotificationFixture(expectedTransitReference)
            .With(i => i.Status, "SUBMITTED")
            .With(i => i.PartOne, new PartOne { ProvideCtcMrn = "YES" })
            .With(i => i.ExternalReferences, [new ExternalReference { System = "NCTS", Reference = expectedMrn }]);

        var resourceEvent = ImportPreNotificationFixtures
            .ImportPreNotificationResourceEventFixture(transitImportPreNotification.Create())
            .Create();

        await SendResourceEventMessageAsync(sqsClient, queueUrl, resourceEvent);
        await WaitForMessageConsumed(sqsClient, queueUrl);

        var matchedGmrsConfig = GetConfig<ImportMatchedGmrsQueueOptions>();
        var (matchedGmrsClient, matchedGmrsQueueUrl) = await GetSqsClient(matchedGmrsConfig.QueueName);

        var matchedGmrEvent = new MatchedGmr { Mrn = expectedMrn, Gmr = GmrFixtures.GmrFixture().Create() };
        var message = new SendMessageRequest
        {
            MessageBody = JsonSerializer.Serialize(matchedGmrEvent),
            QueueUrl = matchedGmrsQueueUrl,
        };

        await matchedGmrsClient.SendMessageAsync(message, TestContext.Current.CancellationToken);
        await WaitForMessageConsumed(matchedGmrsClient, matchedGmrsQueueUrl);

        var item = await AsyncWaiter.WaitForAsync(
            async () =>
            {
                return await Mongo.MatchedImportNotifications.FindOne(
                    p => p.Mrn == expectedMrn && p.Id == expectedTransitReference,
                    TestContext.Current.CancellationToken
                );
            },
            TestContext.Current.CancellationToken
        );

        item.Should().NotBeNull();
        item.Id.Should().BeEquivalentTo(expectedTransitReference);
        item.Mrn.Should().BeEquivalentTo(matchedGmrEvent.Mrn);

        // Then : message is sent to ASB
        var receiver = serviceBusClient.Receiver;
        var serviceBusReceivedMessage = new List<ServiceBusReceivedMessage>();
        var allMessages = await AsyncWaiter.WaitForAsync(
            async () =>
            {
                var receivedMessages = await receiver.ReceiveMessagesAsync(
                    10,
                    TimeSpan.FromSeconds(1),
                    TestContext.Current.CancellationToken
                );
                serviceBusReceivedMessage.AddRange(receivedMessages);
                return serviceBusReceivedMessage.Count >= 1 ? serviceBusReceivedMessage : null;
            },
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(allMessages);
        allMessages.Count.Should().Be(1, "Too many messages received");

        var importMatchMessage = allMessages[0].Body.ToObjectFromJson<ImportMatchMessage>();

        Assert.NotNull(importMatchMessage);
        importMatchMessage.ImportReference.Should().Be(expectedTransitReference);
        importMatchMessage.Match.Should().BeTrue();
    }
}
