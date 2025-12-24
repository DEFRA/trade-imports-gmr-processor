using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Amazon.SQS;
using Amazon.SQS.Model;
using GmrProcessor.Metrics;

namespace GmrProcessor.Consumers;

public abstract class SqsConsumer<TConsumer>(
    ILogger<TConsumer> logger,
    ConsumerMetrics consumerMetrics,
    IAmazonSQS sqsClient,
    string queueName
) : BackgroundService
    where TConsumer : class
{
    [ExcludeFromCodeCoverage]
    protected virtual int WaitTimeSeconds => 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueUrl = (await sqsClient.GetQueueUrlAsync(queueName, stoppingToken)).QueueUrl;

        while (!stoppingToken.IsCancellationRequested)
        {
            var result = await ReceiveMessages(queueUrl, stoppingToken);

            var tasks = result?.Messages?.Select(message => HandleMessage(queueUrl, message, stoppingToken)).ToArray();

            if (tasks is not { Length: > 0 })
                continue;

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task<ReceiveMessageResponse?> ReceiveMessages(string queueUrl, CancellationToken stoppingToken)
    {
        try
        {
            var request = new ReceiveMessageRequest
            {
                MaxNumberOfMessages = 10,
                MessageAttributeNames = ["All"],
                MessageSystemAttributeNames = ["All"],
                QueueUrl = queueUrl,
                VisibilityTimeout = 60,
                WaitTimeSeconds = WaitTimeSeconds,
            };
            return await sqsClient.ReceiveMessageAsync(request, stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to receive messages from {QueueName}", queueName);
            return null;
        }
    }

    private async Task HandleMessage(string queueUrl, Message message, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var success = false;

        try
        {
            await ProcessMessageAsync(message, cancellationToken);
            await sqsClient.DeleteMessageAsync(queueUrl, message.ReceiptHandle, cancellationToken);
            success = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process message {MessageId} from {QueueName}", message.MessageId, queueName);
        }
        finally
        {
            stopwatch.Stop();
            consumerMetrics.RecordProcessDuration(queueName, success, stopwatch.Elapsed);
        }
    }

    protected abstract Task ProcessMessageAsync(Message message, CancellationToken stoppingToken);
}
