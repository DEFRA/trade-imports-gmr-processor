using Amazon.SQS;
using GmrProcessor.Config;
using GmrProcessor.Metrics;
using GmrProcessor.Processors.Gto;
using Microsoft.Extensions.Options;

namespace GmrProcessor.Consumers;

public class GtoMatchedGmrsQueueConsumer(
    ILogger<GtoMatchedGmrsQueueConsumer> logger,
    ConsumerMetrics consumerMetrics,
    IGtoMatchedGmrProcessor gtoMatchedGmrProcessor,
    IOptions<GtoMatchedGmrsQueueOptions> options,
    IAmazonSQS sqsClient
)
    : MatchedGmrsQueueConsumer<GtoMatchedGmrsQueueConsumer, GtoMatchedGmrProcessorResult>(
        logger,
        consumerMetrics,
        gtoMatchedGmrProcessor,
        sqsClient,
        options.Value.QueueName,
        options.Value.WaitTimeSeconds
    );
