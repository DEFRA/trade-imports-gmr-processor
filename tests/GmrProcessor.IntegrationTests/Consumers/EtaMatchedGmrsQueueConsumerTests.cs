using System.Text.Json;
using Amazon.SQS.Model;
using AutoFixture;
using Azure.Messaging.ServiceBus;
using Defra.TradeImportsGmrFinder.Domain.Events;
using GmrProcessor.Config;
using GmrProcessor.Domain.Eta;
using GmrProcessor.IntegrationTests.Clients;
using TestFixtures;

namespace GmrProcessor.IntegrationTests.Consumers;

[Collection("IntegrationTest")]
public class EtaMatchedGmrsQueueConsumerTests(ServiceBusFixture serviceBusFixture)
    : IntegrationTestBase,
        IClassFixture<ServiceBusFixture>
{
    [Fact]
    public async Task WhenMatchedGmrReceived_AnEtaIsSentToIpaffs()
    {
        var serviceBusOptions = GetConfig<TradeImportsServiceBusOptions>();
        var serviceBusClient = serviceBusFixture.GetClient(serviceBusOptions.Eta.QueueName);
        await serviceBusClient.PurgeAsync(TestContext.Current.CancellationToken);

        var expectedMrn = CustomsDeclarationFixtures.GenerateMrn();
        var expectedChedReference = ImportPreNotificationFixtures.GenerateRandomReference();

        var importEvent = await SendImportPreNotificationAsync(expectedChedReference, expectedMrn);
        var expectedImport = importEvent.Resource!;
        var expectedCheckedInDateTime = DateTime.UtcNow;

        var matchedGmrsConfig = GetConfig<EtaMatchedGmrsQueueOptions>();
        var (matchedGmrsClient, matchedGmrsQueueUrl) = await GetSqsClient(matchedGmrsConfig.QueueName);

        var gmrId = GmrFixtures.GenerateGmrId();
        var matchedGmrEvent = new MatchedGmr
        {
            Mrn = expectedMrn,
            Gmr = GmrFixtures
                .GmrFixture()
                .With(g => g.GmrId, gmrId)
                .WithCheckedInCrossingDateTime(expectedCheckedInDateTime)
                .Create(),
        };
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
                return await Mongo.EtaGmr.FindOne(p => p.Id == gmrId, TestContext.Current.CancellationToken);
            },
            TestContext.Current.CancellationToken
        );

        item.Should().NotBeNull();

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

        var importMatchMessage = allMessages[0].Body.ToObjectFromJson<IpaffsUpdatedTimeOfArrivalMessage>();

        Assert.NotNull(importMatchMessage);
        importMatchMessage.ReferenceNumber.Should().Be(expectedImport.ReferenceNumber);
        importMatchMessage.EntryReference.Should().Be(expectedMrn);
        importMatchMessage
            .LocalDateTimeOfArrival.Should()
            .BeCloseTo(expectedCheckedInDateTime, TimeSpan.FromMinutes(1));
    }
}
