using System.Text.Json;
using Amazon.SQS.Model;
using AutoFixture;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using FluentAssertions;
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
        var expectedDeclarationId = "24GB12345678901234";

        var importPreNotification = new ImportPreNotification
        {
            Status = expectedStatus,
            PartOne = new PartOne { ProvideCtcMrn = "YES" },
            ExternalReferences = [new ExternalReference { System = "NCTS", Reference = expectedDeclarationId }],
        };

        var resourceEvent = ImportPreNotificationFixtures
            .ImportPreNotificationResourceEventFixture(importPreNotification)
            .With(r => r.ResourceId, expectedChed)
            .Create();

        await SendMessageAsync(sqsClient, queueUrl, resourceEvent);
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
        importTransitCreated.DeclarationId.Should().Be(expectedDeclarationId);
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

        await SendMessageAsync(sqsClient, queueUrl, initialResourceEvent);
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
        var updatedDeclarationId = "24GB11111111111111";

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

        await SendMessageAsync(sqsClient, queueUrl, updatedResourceEvent);
        await WaitForMessageConsumed(sqsClient, queueUrl);

        await Task.Delay(500, TestContext.Current.CancellationToken);

        var importTransitUpdated = await Mongo.ImportTransits.FindOne(
            p => p.Id == expectedChed,
            TestContext.Current.CancellationToken
        );

        importTransitUpdated.Should().NotBeNull("Import Transit was not found");
        importTransitUpdated.TransitOverrideRequired.Should().Be(expectedTransitOverrideRequired);
        importTransitUpdated.DeclarationId.Should().Be(updatedDeclarationId);
    }

    private static async Task SendMessageAsync<T>(Amazon.SQS.IAmazonSQS sqsClient, string queueUrl, T resourceEvent)
        where T : class
    {
        var message = new SendMessageRequest
        {
            MessageBody = JsonSerializer.Serialize(resourceEvent),
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                {
                    "ResourceType",
                    new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = (resourceEvent as dynamic).ResourceType,
                    }
                },
            },
            QueueUrl = queueUrl,
        };

        await sqsClient.SendMessageAsync(message, TestContext.Current.CancellationToken);
    }

    private static async Task WaitForMessageConsumed(Amazon.SQS.IAmazonSQS sqsClient, string queueUrl)
    {
        var messageConsumed = await AsyncWaiter.WaitForAsync(
            async () =>
            {
                var result = await sqsClient.GetQueueAttributesAsync(
                    queueUrl,
                    ["ApproximateNumberOfMessages", "ApproximateNumberOfMessagesNotVisible"]
                );

                var numberMessagesOnQueue =
                    result.ApproximateNumberOfMessages + result.ApproximateNumberOfMessagesNotVisible;

                return numberMessagesOnQueue == 0 ? (int?)numberMessagesOnQueue : null;
            },
            TestContext.Current.CancellationToken
        );

        messageConsumed.Should().NotBeNull("Message was not consumed");
    }
}
