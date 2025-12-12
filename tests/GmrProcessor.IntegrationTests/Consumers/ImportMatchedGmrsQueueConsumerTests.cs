using System.Text.Json;
using Amazon.SQS.Model;
using AutoFixture;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using Defra.TradeImportsGmrFinder.Domain.Events;
using GmrProcessor.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TestFixtures;

namespace GmrProcessor.IntegrationTests.Consumers;

[Collection("UsesWireMockClient")]
public class ImportMatchedGmrsQueueConsumerTests(WireMockClient wireMockClient) : IntegrationTestBase
{
    [Fact]
    public async Task WhenMatchedGmrReceived_MatchIsCreatedFromImports()
    {
        var expectedMrn = CustomsDeclarationFixtures.GenerateMrn();
        var expectedImport = ImportPreNotificationFixtures
            .ImportPreNotificationFixture(ImportPreNotificationFixtures.GenerateRandomReference())
            .Create();

        await wireMockClient.MockImportPreNotificationsByMrn(
            expectedMrn,
            ImportPreNotificationFixtures.ImportPreNotificationResponseFixture(expectedImport).Create()
        );

        var matchedGmrsConfig = ServiceProvider.GetRequiredService<IOptions<ImportMatchedGmrsQueueOptions>>().Value;
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
                    p => p.Mrn == expectedMrn && p.Id == expectedImport.ReferenceNumber,
                    TestContext.Current.CancellationToken
                );
            },
            TestContext.Current.CancellationToken
        );

        item.Should().NotBeNull();
        item.Id.Should().BeEquivalentTo(expectedImport.ReferenceNumber);
        item.Mrn.Should().BeEquivalentTo(matchedGmrEvent.Mrn);
    }

    [Fact]
    public async Task WhenMatchedGmrReceived_MatchIsCreatedFromTransits()
    {
        var expectedMrn = CustomsDeclarationFixtures.GenerateMrn();
        var expectedTransit = ImportPreNotificationFixtures.GenerateRandomReference();

        await wireMockClient.MockImportPreNotificationsByMrn(expectedMrn);

        var dataEventsQueueConfig = GetConfig<GtoDataEventsQueueConsumerOptions>();
        var (sqsClient, queueUrl) = await GetSqsClient(dataEventsQueueConfig.QueueName);

        var transitImportPreNotification = ImportPreNotificationFixtures
            .ImportPreNotificationFixture(expectedTransit)
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
                    p => p.Mrn == expectedMrn && p.Id == expectedTransit,
                    TestContext.Current.CancellationToken
                );
            },
            TestContext.Current.CancellationToken
        );

        item.Should().NotBeNull();
        item.Id.Should().BeEquivalentTo(expectedTransit);
        item.Mrn.Should().BeEquivalentTo(matchedGmrEvent.Mrn);
    }
}
