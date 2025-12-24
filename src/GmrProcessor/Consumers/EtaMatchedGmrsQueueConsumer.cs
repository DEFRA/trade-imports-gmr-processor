using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.TradeImportsGmrFinder.Domain.Events;
using GmrProcessor.Config;
using GmrProcessor.Extensions;
using GmrProcessor.Metrics;
using GmrProcessor.Processors.Eta;
using GmrProcessor.Utils;
using Microsoft.Extensions.Options;

namespace GmrProcessor.Consumers;

public class EtaMatchedGmrsQueueConsumer(
    ILogger<EtaMatchedGmrsQueueConsumer> logger,
    ConsumerMetrics consumerMetrics,
    IEtaMatchedGmrProcessor etaMatchedGmrProcessor,
    IOptions<EtaMatchedGmrsQueueOptions> options,
    IAmazonSQS sqsClient
)
    : MatchedGmrsQueueConsumer<EtaMatchedGmrsQueueConsumer, EtaMatchedGmrProcessorResult>(
        logger,
        consumerMetrics,
        etaMatchedGmrProcessor,
        sqsClient,
        options.Value.QueueName,
        options.Value.WaitTimeSeconds
    );
