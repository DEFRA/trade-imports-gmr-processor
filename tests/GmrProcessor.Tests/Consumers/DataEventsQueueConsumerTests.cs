using System.Reflection;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using AutoFixture;
using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using GmrProcessor.Config;
using GmrProcessor.Consumers;
using GmrProcessor.Extensions;
using GmrProcessor.Processors.Gto;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TestFixtures;

namespace GmrProcessor.Tests.Consumers;

public class DataEventsQueueConsumerTests
{
    private readonly Mock<IGtoImportPreNotificationProcessor> _importPreNotificationProcessor = new();
    private readonly DataEventsQueueConsumer _consumer;

    public DataEventsQueueConsumerTests()
    {
        _consumer = new DataEventsQueueConsumer(
            NullLogger<DataEventsQueueConsumer>.Instance,
            new Mock<IAmazonSQS>().Object,
            Options.Create(
                new DataEventsQueueConsumerOptions
                {
                    QueueName = "trade_imports_data_upserted_gmr_processor_gto",
                    WaitTimeSeconds = 1,
                }
            ),
            _importPreNotificationProcessor.Object
        );
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenResourceTypeIsImportPreNotification_SendsToImportPreNotificationProcessor()
    {
        var body = JsonSerializer.Serialize(
            ImportPreNotificationFixtures
                .ImportPreNotificationResourceEventFixture(
                    ImportPreNotificationFixtures.ImportPreNotificationFixture().Create()
                )
                .Create()
        );

        var message = new Message
        {
            Body = body,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                [SqsMessageHeaders.ResourceType] = new()
                {
                    DataType = "String",
                    StringValue = ResourceEventResourceTypes.ImportPreNotification,
                },
            },
        };

        await InvokeProcessMessageAsync(message, CancellationToken.None);

        _importPreNotificationProcessor.Verify(
            processor =>
                processor.ProcessAsync(It.IsAny<ResourceEvent<ImportPreNotification>>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenResourceTypeUnhandled_DoesNothing()
    {
        var message = new Message
        {
            Body = "{}",
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                [SqsMessageHeaders.ResourceType] = new() { DataType = "String", StringValue = "Unknown" },
            },
        };

        await InvokeProcessMessageAsync(message, CancellationToken.None);

        _importPreNotificationProcessor.Verify(
            processor =>
                processor.ProcessAsync(It.IsAny<ResourceEvent<ImportPreNotification>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    private Task InvokeProcessMessageAsync(Message message, CancellationToken cancellationToken)
    {
        var method = typeof(DataEventsQueueConsumer).GetMethod(
            "ProcessMessageAsync",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;

        return (Task)method.Invoke(_consumer, [message, cancellationToken])!;
    }
}
