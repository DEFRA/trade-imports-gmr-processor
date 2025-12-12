using Amazon.SQS;
using GmrProcessor.Config;
using GmrProcessor.Processors.Gto;
using Microsoft.Extensions.Options;

namespace GmrProcessor.Consumers;

public class GtoMatchedGmrsQueueConsumer(
    ILogger<GtoMatchedGmrsQueueConsumer> logger,
    IGtoMatchedGmrProcessor gtoMatchedGmrProcessor,
    IOptions<GtoMatchedGmrsQueueOptions> options,
    IAmazonSQS sqsClient
)
    : MatchedGmrsQueueConsumer<GtoMatchedGmrsQueueConsumer, GtoMatchedGmrProcessorResult>(
        logger,
        gtoMatchedGmrProcessor,
        sqsClient,
        options.Value.QueueName,
        options.Value.WaitTimeSeconds
    );
