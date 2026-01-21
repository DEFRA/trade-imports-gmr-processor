using System.Reflection;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.TradeImportsDataApi.Domain.CustomsDeclaration;
using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using GmrProcessor.Config;
using GmrProcessor.Consumers;
using GmrProcessor.Extensions;
using GmrProcessor.Metrics;
using GmrProcessor.Processors.Gto;
using GmrProcessor.Tests.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TestFixtures;

namespace GmrProcessor.Tests.Consumers;

public class DataEventsQueueConsumerTests
{
    private readonly Mock<IGtoImportPreNotificationProcessor> _importPreNotificationProcessor = new();
    private readonly Mock<IMrnChedMatchProcessor> _mrnChedMatchProcessor = new();
    private readonly DataEventsQueueConsumer _consumer;

    public DataEventsQueueConsumerTests()
    {
        _consumer = new DataEventsQueueConsumer(
            NullLogger<DataEventsQueueConsumer>.Instance,
            new ConsumerMetrics(MockMeterFactory.Create()),
            new Mock<IAmazonSQS>().Object,
            Options.Create(
                new GtoDataEventsQueueConsumerOptions
                {
                    QueueName = "trade_imports_data_upserted_gmr_processor_gto",
                    WaitTimeSeconds = 1,
                }
            ),
            _importPreNotificationProcessor.Object,
            _mrnChedMatchProcessor.Object
        );
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenResourceTypeIsImportPreNotification_SendsToImportPreNotificationProcessor()
    {
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var body = JsonSerializer.Serialize(
            ImportPreNotificationFixtures
                .ImportPreNotificationResourceEventFixture(
                    ImportPreNotificationFixtures
                        .ImportPreNotificationFixture(ImportPreNotificationFixtures.GenerateRandomReference())
                        .Create()
                )
                .Create(),
            serializerOptions
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
                processor.Process(It.IsAny<ResourceEvent<ImportPreNotification>>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenResourceTypeIsCustomsDeclaration_SendsToMrnChedMatchProcessor()
    {
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var body = JsonSerializer.Serialize(
            CustomsDeclarationFixtures
                .CustomsDeclarationResourceEventFixture(CustomsDeclarationFixtures.CustomsDeclarationFixture().Create())
                .Create(),
            serializerOptions
        );

        var message = new Message
        {
            Body = body,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                [SqsMessageHeaders.ResourceType] = new()
                {
                    DataType = "String",
                    StringValue = ResourceEventResourceTypes.CustomsDeclaration,
                },
            },
        };

        await InvokeProcessMessageAsync(message, CancellationToken.None);

        _mrnChedMatchProcessor.Verify(
            processor =>
                processor.ProcessCustomsDeclaration(
                    It.IsAny<ResourceEvent<CustomsDeclaration>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        _importPreNotificationProcessor.Verify(
            processor =>
                processor.Process(It.IsAny<ResourceEvent<ImportPreNotification>>(), It.IsAny<CancellationToken>()),
            Times.Never
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
                processor.Process(It.IsAny<ResourceEvent<ImportPreNotification>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenDeserializationFails_ThrowsJsonException()
    {
        var message = new Message
        {
            Body = "{\"invalid\": \"structure\"}",
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                [SqsMessageHeaders.ResourceType] = new()
                {
                    DataType = "String",
                    StringValue = ResourceEventResourceTypes.ImportPreNotification,
                },
            },
        };

        var act = async () => await InvokeProcessMessageAsync(message, CancellationToken.None);

        await act.Should().ThrowAsync<JsonException>().WithMessage("Failed to deserialise JSON to*");

        _importPreNotificationProcessor.Verify(
            processor =>
                processor.Process(It.IsAny<ResourceEvent<ImportPreNotification>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );

        _mrnChedMatchProcessor.Verify(
            processor =>
                processor.ProcessCustomsDeclaration(
                    It.IsAny<ResourceEvent<CustomsDeclaration>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public void Deserialize_WithCamelCasePayload_DefaultOptionsWouldHaveFailed()
    {
        var reference = ImportPreNotificationFixtures.GenerateRandomReference();
        var defaultJsonSerializerOptions = new JsonSerializerOptions();
        var webJsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var payload = JsonSerializer.Serialize(
            ImportPreNotificationFixtures
                .ImportPreNotificationResourceEventFixture(
                    ImportPreNotificationFixtures.ImportPreNotificationFixture(reference).Create()
                )
                .Create(),
            webJsonSerializerOptions
        );

        payload.Should().Contain("\"resourceId\"");
        payload.Should().Contain("\"operation\"");

        var act = () =>
            JsonSerializer.Deserialize<ResourceEvent<ImportPreNotification>>(payload, defaultJsonSerializerOptions);

        act.Should().Throw<JsonException>().Which.Message.Should().Contain("missing required properties");

        var deserialised = JsonSerializer.Deserialize<ResourceEvent<ImportPreNotification>>(
            payload,
            webJsonSerializerOptions
        );

        deserialised.Should().NotBeNull();
        deserialised.ResourceId.Should().Be(reference);
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
