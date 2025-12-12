using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using GmrProcessor.Config;
using GmrProcessor.Extensions;
using GmrProcessor.Processors.Gto;
using GmrProcessor.Utils;
using Microsoft.Extensions.Options;

namespace GmrProcessor.Consumers;

public sealed class GtoDataEventsQueueConsumer(
    ILogger<GtoDataEventsQueueConsumer> logger,
    IAmazonSQS sqsClient,
    IOptions<GtoDataEventsQueueConsumerOptions> options,
    IGtoImportPreNotificationProcessor importPreNotificationProcessor
) : SqsConsumer<GtoDataEventsQueueConsumer>(logger, sqsClient, options.Value.QueueName)
{
    private readonly ILogger<GtoDataEventsQueueConsumer> _logger = logger;

    protected override int WaitTimeSeconds { get; } = options.Value.WaitTimeSeconds;

    protected override async Task ProcessMessageAsync(Message message, CancellationToken stoppingToken)
    {
        var json = MessageDeserializer.Deserialize<JsonElement>(message.Body, message.GetContentEncoding());

        _logger.LogInformation("Message received: {ResourceType}", message.GetResourceType());

        switch (message.GetResourceType())
        {
            case ResourceEventResourceTypes.ImportPreNotification:
                var importPreNotification = json.Deserialize<ResourceEvent<ImportPreNotification>>()!;
                await importPreNotificationProcessor.ProcessAsync(importPreNotification, stoppingToken);
                break;
        }
    }
}
