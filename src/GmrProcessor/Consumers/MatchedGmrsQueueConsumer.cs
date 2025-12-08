using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.TradeImportsGmrFinder.Domain.Events;
using GmrProcessor.Extensions;
using GmrProcessor.Processors;
using GmrProcessor.Utils;

namespace GmrProcessor.Consumers;

public abstract class MatchedGmrsQueueConsumer<TConsumer>(
    ILogger<TConsumer> logger,
    IMatchedGmrProcessor processor,
    IAmazonSQS sqsClient,
    string queueName,
    int waitTimeSeconds = 20
) : SqsConsumer<TConsumer>(logger, sqsClient, queueName)
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

        _logger.LogInformation("Received matched GMR: {Identifier}", matchedGmr.GetIdentifier);

        await processor.Process(matchedGmr, stoppingToken);
    }
}
