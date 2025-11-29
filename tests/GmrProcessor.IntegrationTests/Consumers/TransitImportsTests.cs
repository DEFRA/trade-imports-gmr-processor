using System.Text.Json;
using Amazon.SQS.Model;
using AutoFixture;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using GmrProcessor.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TestFixtures;

namespace GmrProcessor.IntegrationTests.Consumers;

public class TransitImportsTests : IntegrationTestBase
{
    [Fact]
    public async Task WhenImportPreNotificationReceived_ShouldInsertWithCorrectValues()
    {
        var config = ServiceProvider.GetRequiredService<IOptions<DataEventsQueueConsumerOptions>>().Value;
        var (sqsClient, queueUrl) = await GetSqsClient(config.QueueName);

        var expectedChed = ImportPreNotificationFixtures.GenerateRandomReference();
        var expectedStatus = "SUBMITTED";
        var expectedTransitOverrideRequired = false;
        var expectedMrn = "24GB12345678901234";

        var importPreNotification = new ImportPreNotification
        {
            Status = expectedStatus,
            PartOne = new PartOne { ProvideCtcMrn = "YES" },
            ExternalReferences = [new ExternalReference { System = "NCTS", Reference = expectedMrn }],
        };

        var resourceEvent = ImportPreNotificationFixtures
            .ImportPreNotificationResourceEventFixture(importPreNotification)
            .With(r => r.ResourceId, expectedChed)
            .Create();

        await SendResourceEventMessageAsync(sqsClient, queueUrl, resourceEvent);
        await WaitForMessageConsumed(sqsClient, queueUrl);

        var importTransitCreated = await AsyncWaiter.WaitForAsync(
            async () =>
            {
                return await Mongo.ImportTransits.FindOne(
                    p => p.Id == expectedChed,
                    TestContext.Current.CancellationToken
                );
            },
            TestContext.Current.CancellationToken
        );

        importTransitCreated.Should().NotBeNull("Import Transit was not created");
        importTransitCreated.TransitOverrideRequired.Should().Be(expectedTransitOverrideRequired);
        importTransitCreated.Mrn.Should().Be(expectedMrn);
    }

    [Fact]
    public async Task WhenImportPreNotificationReceived_ShouldUpdateExistingValues()
    {
        var config = ServiceProvider.GetRequiredService<IOptions<DataEventsQueueConsumerOptions>>().Value;
        var (sqsClient, queueUrl) = await GetSqsClient(config.QueueName);

        var expectedChed = ImportPreNotificationFixtures.GenerateRandomReference();
        var expectedDeclration = "24GB11111111111111";

        // First message - initial insert

        var initialImportPreNotification = new ImportPreNotification
        {
            Status = "SUBMITTED",
            PartOne = new PartOne { ProvideCtcMrn = "YES" },
            PartTwo = new PartTwo { InspectionRequired = "Required" },
            ExternalReferences = [new ExternalReference { System = "NCTS", Reference = expectedDeclration }],
        };

        var initialResourceEvent = ImportPreNotificationFixtures
            .ImportPreNotificationResourceEventFixture(initialImportPreNotification)
            .With(r => r.ResourceId, expectedChed)
            .Create();

        await SendResourceEventMessageAsync(sqsClient, queueUrl, initialResourceEvent);
        await WaitForMessageConsumed(sqsClient, queueUrl);

        // Wait for initial insert
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

        // Second message - update with new values
        var updatedStatus = "VALIDATED";
        var updatedMrn = "24GB11111111111111";

        var expectedTransitOverrideRequired = false;

        var updatedImportPreNotification = new ImportPreNotification
        {
            Status = updatedStatus,
            PartOne = new PartOne { ProvideCtcMrn = "YES" },
            PartTwo = new PartTwo { InspectionRequired = "Required" },
            ExternalReferences = [new ExternalReference { System = "NCTS", Reference = expectedDeclration }],
        };

        var updatedResourceEvent = ImportPreNotificationFixtures
            .ImportPreNotificationResourceEventFixture(updatedImportPreNotification)
            .With(r => r.ResourceId, expectedChed)
            .Create();

        await SendResourceEventMessageAsync(sqsClient, queueUrl, updatedResourceEvent);
        await WaitForMessageConsumed(sqsClient, queueUrl);

        await Task.Delay(500, TestContext.Current.CancellationToken);

        var importTransitUpdated = await Mongo.ImportTransits.FindOne(
            p => p.Id == expectedChed,
            TestContext.Current.CancellationToken
        );

        importTransitUpdated.Should().NotBeNull("Import Transit was not found");
        importTransitUpdated.TransitOverrideRequired.Should().Be(expectedTransitOverrideRequired);
        importTransitUpdated.Mrn.Should().Be(updatedMrn);
    }
}
