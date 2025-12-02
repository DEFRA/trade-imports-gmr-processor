using System.Text.Json;
using Amazon.SQS.Model;
using AutoFixture;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using Defra.TradeImportsGmrFinder.Domain.Events;
using FluentAssertions;
using GmrProcessor.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TestFixtures;

namespace GmrProcessor.IntegrationTests.Consumers;

public class GtoMatchedGmrsQueueConsumerTests : IntegrationTestBase
{
    [Fact]
    public async Task WhenMatchedGmrReceived_AndIsNewMatch_IsStored()
    {
        var expectedMrn = CustomsDeclarationFixtures.GenerateMrn();
        var expectedChed = ImportPreNotificationFixtures.GenerateRandomReference();

        var importTransitConfig = ServiceProvider.GetRequiredService<IOptions<DataEventsQueueConsumerOptions>>().Value;
        var (importTransitClient, importTransitQueueUrl) = await GetSqsClient(importTransitConfig.QueueName);

        var importPreNotification = new ImportPreNotification
        {
            Status = "SUBMITTED",
            PartOne = new PartOne { ProvideCtcMrn = "YES" },
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
                return await Mongo.GtoMatchedGmrItem.FindOne(
                    p => p.Mrn == expectedMrn && p.GmrId == matchedGmrEvent.Gmr.GmrId,
                    TestContext.Current.CancellationToken
                );
            },
            TestContext.Current.CancellationToken
        );

        item.Should().NotBeNull();
        item.Gmr.Should().BeEquivalentTo(matchedGmrEvent.Gmr);
    }
}
