using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.TradeImportsGmrFinder.Domain.Events;
using GmrProcessor.Extensions;
using GmrProcessor.Metrics;
using GmrProcessor.Processors;
using GmrProcessor.Utils;

namespace GmrProcessor.Consumers;

public abstract class MatchedGmrsQueueConsumer<TConsumer, T>(
    ILogger<TConsumer> logger,
    ConsumerMetrics consumerMetrics,
    IMatchedGmrProcessor<T> processor,
    IAmazonSQS sqsClient,
    string queueName,
    int waitTimeSeconds = 20
) : SqsConsumer<TConsumer>(logger, consumerMetrics, sqsClient, queueName)
    where TConsumer : class
{
    private readonly ILogger<TConsumer> _logger = logger;
    protected override int WaitTimeSeconds { get; } = waitTimeSeconds;

    protected override async Task ProcessMessageAsync(Message message, CancellationToken stoppingToken)
    {
        var matchedGmr = MessageDeserializer.Deserialize<MatchedGmr>(message.Body, message.GetContentEncoding());
        if (matchedGmr == null)
        {
            throw new JsonException("Unable to deserialise MatchedGmr message");
        }

        using (
            _logger.BeginScope(
                new Dictionary<string, object>
                {
                    ["event.reference"] = matchedGmr.Gmr.GmrId,
                    ["event.type"] = "matched-gmr",
                    ["event.provider"] = GetType().Name,
                }
            )
        )
        {
            _logger.LogInformation(
                "{Consumer} received matched GMR {Gmr} to Mrn {Mrn}",
                typeof(TConsumer).Name,
                matchedGmr.Gmr,
                matchedGmr.Mrn
            );

            await processor.Process(matchedGmr, stoppingToken);
        }
    }
}
