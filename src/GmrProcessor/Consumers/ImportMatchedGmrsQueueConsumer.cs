using Amazon.SQS;
using GmrProcessor.Config;
using GmrProcessor.Metrics;
using GmrProcessor.Processors.Gto;
using GmrProcessor.Processors.ImportGmrMatching;
using Microsoft.Extensions.Options;

namespace GmrProcessor.Consumers;

public class ImportMatchedGmrsQueueConsumer(
    ILogger<ImportMatchedGmrsQueueConsumer> logger,
    ConsumerMetrics consumerMetrics,
    IImportMatchedGmrsProcessor importMatchedGmrsProcessor,
    IOptions<ImportMatchedGmrsQueueOptions> options,
    IAmazonSQS sqsClient
)
    : MatchedGmrsQueueConsumer<ImportMatchedGmrsQueueConsumer, object>(
        logger,
        consumerMetrics,
        importMatchedGmrsProcessor,
        sqsClient,
        options.Value.QueueName,
        options.Value.WaitTimeSeconds
    );
