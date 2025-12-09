using Amazon.SQS;
using GmrProcessor.Config;
using GmrProcessor.Processors.Gto;
using Microsoft.Extensions.Options;

namespace GmrProcessor.Consumers;

public class ImportMatchedGmrsQueueConsumer(
    ILogger<ImportMatchedGmrsQueueConsumer> logger,
    IImportMatchedGmrsProcessor importMatchedGmrsProcessor,
    IOptions<ImportMatchedGmrsQueueOptions> options,
    IAmazonSQS sqsClient
)
    : MatchedGmrsQueueConsumer<ImportMatchedGmrsQueueConsumer, object>(
        logger,
        importMatchedGmrsProcessor,
        sqsClient,
        options.Value.QueueName,
        options.Value.WaitTimeSeconds
    );
