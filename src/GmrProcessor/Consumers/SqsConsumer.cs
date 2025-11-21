using System.Diagnostics.CodeAnalysis;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace GmrProcessor.Consumers;

public abstract class SqsConsumer<TConsumer>(ILogger<TConsumer> logger, IAmazonSQS sqsClient, string queueName)
    : BackgroundService
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
        try
        {
            await ProcessMessageAsync(message, cancellationToken);
            await sqsClient.DeleteMessageAsync(queueUrl, message.ReceiptHandle, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process message {MessageId} from {QueueName}", message.MessageId, queueName);
        }
    }

    protected abstract Task ProcessMessageAsync(Message message, CancellationToken stoppingToken);
}
