using System.Text.Json;
using Amazon.SQS.Model;
using AutoFixture;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using Defra.TradeImportsGmrFinder.Domain.Events;
using GmrProcessor.Config;
using GmrProcessor.Data;
using GmrProcessor.Data.Gto;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TestFixtures;

namespace GmrProcessor.IntegrationTests.Consumers;

public class GtoMatchedGmrsQueueConsumerTests : IntegrationTestBase
{
    [Fact]
    public async Task WhenMatchedGmrReceived_AndIsNewMatch_IsStored()
    {
        var expectedMrn = CustomsDeclarationFixtures.GenerateMrn();
        var expectedChed = ImportPreNotificationFixtures.GenerateRandomReference();
        const string expectedGmr = "GMRA44448881"; // GVMS test API fixture

        await Mongo.GtoGmr.DeleteOneAsync(
            Builders<GtoGmr>.Filter.Where(f => f.Id == expectedGmr),
            TestContext.Current.CancellationToken
        );

        var importTransitConfig = ServiceProvider.GetRequiredService<IOptions<DataEventsQueueConsumerOptions>>().Value;
        var (importTransitClient, importTransitQueueUrl) = await GetSqsClient(importTransitConfig.QueueName);

        var importPreNotification = new ImportPreNotification
        {
            Status = "SUBMITTED",
            PartOne = new PartOne { ProvideCtcMrn = "YES" },
            PartTwo = new PartTwo { InspectionRequired = "required" },
            ExternalReferences = [new ExternalReference { System = "NCTS", Reference = expectedMrn }],
        };

        var importPreNotificationResourceEvent = ImportPreNotificationFixtures
            .ImportPreNotificationResourceEventFixture(importPreNotification)
            .With(r => r.ResourceId, expectedChed)
            .Create();

        await SendResourceEventMessageAsync(
            importTransitClient,
            importTransitQueueUrl,
            importPreNotificationResourceEvent
        );
        await WaitForMessageConsumed(importTransitClient, importTransitQueueUrl);

        await AsyncWaiter.WaitForAsync(
            async () =>
            {
                return await Mongo.ImportTransits.FindOne(
                    p => p.Id == expectedChed,
                    TestContext.Current.CancellationToken
                );
            },
            TestContext.Current.CancellationToken
        );

        var matchedGmrsConfig = ServiceProvider.GetRequiredService<IOptions<GtoMatchedGmrsQueueOptions>>().Value;
        var (matchedGmrsClient, matchedGmrsQueueUrl) = await GetSqsClient(matchedGmrsConfig.QueueName);

        var matchedGmrEvent = new MatchedGmr
        {
            Mrn = expectedMrn,
            Gmr = GmrFixtures.GmrFixture().With(g => g.GmrId, expectedGmr).Create(),
        };
        var message = new SendMessageRequest
        {
            MessageBody = JsonSerializer.Serialize(matchedGmrEvent),
            QueueUrl = matchedGmrsQueueUrl,
        };

        await matchedGmrsClient.SendMessageAsync(message, TestContext.Current.CancellationToken);
        await WaitForMessageConsumed(matchedGmrsClient, matchedGmrsQueueUrl);

        var matchedGmrItem = await AsyncWaiter.WaitForAsync(
            async () =>
            {
                return await Mongo.GtoMatchedGmrItem.FindOne(
                    p => p.Mrn == expectedMrn && p.GmrId == matchedGmrEvent.Gmr.GmrId,
                    TestContext.Current.CancellationToken
                );
            },
            TestContext.Current.CancellationToken
        );

        matchedGmrItem.Should().NotBeNull();

        var gtoGmrRecord = await AsyncWaiter.WaitForAsync(
            async () =>
            {
                return await Mongo.GtoGmr.FindOne(
                    g => g.Gmr.GmrId == expectedGmr && g.HoldStatus,
                    TestContext.Current.CancellationToken
                );
            },
            TestContext.Current.CancellationToken
        );

        gtoGmrRecord.Should().NotBeNull();
    }
}
