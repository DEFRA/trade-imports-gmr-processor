using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using GmrProcessor.Config;
using GmrProcessor.Extensions;
using GmrProcessor.Metrics;
using GmrProcessor.Processors.Gto;
using GmrProcessor.Utils;
using Microsoft.Extensions.Options;

namespace GmrProcessor.Consumers;

public sealed class GtoDataEventsQueueConsumer(
    ILogger<GtoDataEventsQueueConsumer> logger,
    ConsumerMetrics consumerMetrics,
    IAmazonSQS sqsClient,
    IOptions<GtoDataEventsQueueConsumerOptions> options,
    IGtoImportPreNotificationProcessor importPreNotificationProcessor
) : SqsConsumer<GtoDataEventsQueueConsumer>(logger, consumerMetrics, sqsClient, options.Value.QueueName)
{
    private static readonly JsonSerializerOptions s_defaultSerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ILogger<GtoDataEventsQueueConsumer> _logger = logger;

    protected override int WaitTimeSeconds { get; } = options.Value.WaitTimeSeconds;

    protected override async Task ProcessMessageAsync(Message message, CancellationToken stoppingToken)
    {
        var json = MessageDeserializer.Deserialize<JsonElement>(message.Body, message.GetContentEncoding());

        _logger.LogInformation("Message received: {ResourceType}", message.GetResourceType());

        switch (message.GetResourceType())
        {
            case ResourceEventResourceTypes.ImportPreNotification:
                var importPreNotification = DeserializeAsync<ResourceEvent<ImportPreNotification>>(json)!;
                await importPreNotificationProcessor.ProcessAsync(importPreNotification, stoppingToken);
                break;
        }
    }

    private T? DeserializeAsync<T>(JsonElement json)
    {
        try
        {
            return json.Deserialize<T>(s_defaultSerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Failed to deserialise JSON to {Type}: {Json}", typeof(T).FullName, json.GetRawText());
            throw new JsonException($"Failed to deserialise JSON to {typeof(T).FullName}.", ex);
        }
    }
}
