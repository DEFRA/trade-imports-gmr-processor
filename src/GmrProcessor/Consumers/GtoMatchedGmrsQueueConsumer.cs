using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.TradeImportsGmrFinder.Domain.Events;
using GmrProcessor.Config;
using GmrProcessor.Extensions;
using GmrProcessor.Processors.Gto;
using GmrProcessor.Utils;
using Microsoft.Extensions.Options;

namespace GmrProcessor.Consumers;

public class GtoMatchedGmrsQueueConsumer(
    ILogger<GtoMatchedGmrsQueueConsumer> logger,
    IGtoMatchedGmrProcessor gtoMatchedGmrProcessor,
    IOptions<GtoMatchedGmrsQueueOptions> options,
    IAmazonSQS sqsClient
) : SqsConsumer<GtoMatchedGmrsQueueConsumer>(logger, sqsClient, options.Value.QueueName)
{
    private readonly ILogger<GtoMatchedGmrsQueueConsumer> _logger = logger;
    protected override int WaitTimeSeconds { get; } = options.Value.WaitTimeSeconds;

    protected override async Task ProcessMessageAsync(Message message, CancellationToken stoppingToken)
    {
        var matchedGmr = MessageDeserializer.Deserialize<MatchedGmr>(message.Body, message.GetContentEncoding());
        if (matchedGmr == null)
        {
            throw new JsonException("Unable to deserialise MatchedGmr message");
        }

        _logger.LogInformation("Received matched GMR: {Identifier}", matchedGmr.GetIdentifier);

        await gtoMatchedGmrProcessor.Process(matchedGmr, stoppingToken);
    }
}
