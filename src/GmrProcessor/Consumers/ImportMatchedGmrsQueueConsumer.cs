using System.Diagnostics.CodeAnalysis;
using Amazon.SQS;
using GmrProcessor.Config;
using GmrProcessor.Metrics;
using GmrProcessor.Processors.Gto;
using GmrProcessor.Processors.ImportGmrMatching;
using Microsoft.Extensions.Options;

namespace GmrProcessor.Consumers;

[ExcludeFromCodeCoverage]
public class ImportMatchedGmrsQueueConsumer(
    ILogger<ImportMatchedGmrsQueueConsumer> logger,
    ConsumerMetrics consumerMetrics,
    IImportMatchedGmrsProcessor importMatchedGmrsProcessor,
    IOptions<ImportMatchedGmrsQueueOptions> options,
    IAmazonSQS sqsClient
)
    : MatchedGmrsQueueConsumer<ImportMatchedGmrsQueueConsumer, ImportMatchedGmrsProcessorResult>(
        logger,
        consumerMetrics,
        importMatchedGmrsProcessor,
        sqsClient,
        options.Value.QueueName,
        options.Value.WaitTimeSeconds
    );
