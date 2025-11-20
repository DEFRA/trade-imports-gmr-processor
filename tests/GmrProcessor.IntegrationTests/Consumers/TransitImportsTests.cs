using System.Text.Json;
using Amazon.SQS.Model;
using AutoFixture;
using Defra.TradeImportsDataApi.Domain.Events;
using FluentAssertions;
using GmrProcessor.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TestFixtures;

namespace GmrProcessor.IntegrationTests.Consumers;

public class TransitImportsTests : IntegrationTestBase
{
    [Fact]
    public async Task WhenCustomsDeclarationReceived_ShouldBeProcessed()
    {
        var config = ServiceProvider.GetRequiredService<IOptions<DataEventsQueueConsumerOptions>>().Value;
        var (sqsClient, queueUrl) = await GetSqsClient(config.QueueName);

        var expectedChed = ImportPreNotificationFixtures.GenerateRandomReference();

        var customsDeclaration = ImportPreNotificationFixtures.ImportPreNotificationFixture().Create();
        var resourceEvent = ImportPreNotificationFixtures
            .ImportPreNotificationResourceEventFixture(customsDeclaration)
            .With(r => r.ResourceId, expectedChed)
            .Create();

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
                        StringValue = ResourceEventResourceTypes.CustomsDeclaration,
                    }
                },
            },
            QueueUrl = queueUrl,
        };

        await sqsClient.SendMessageAsync(message, TestContext.Current.CancellationToken);

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
    }
}
