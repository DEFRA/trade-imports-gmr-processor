using Amazon.SQS;
using Amazon.SQS.Model;
using FluentAssertions;
using GmrProcessor.Consumers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GmrProcessor.Tests.Consumers;

public class SqsConsumerTests
{
    private readonly ILogger<TestConsumer> _logger = NullLogger<TestConsumer>.Instance;
    private readonly Mock<IAmazonSQS> _mockSqsClient = new();
    private const string QueueName = "queue_name";
    private const string QueueUrl = "http://queue-url";
    private static readonly TimeSpan s_testTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan s_cancellationTokenCancelAfter = TimeSpan.FromSeconds(1);

    private readonly Message _message = new()
    {
        MessageId = Guid.NewGuid().ToString(),
        ReceiptHandle = Guid.NewGuid().ToString(),
        Body = "payload",
    };

    private TestConsumer _consumer;

    public SqsConsumerTests()
    {
        _consumer = new TestConsumer(_logger, _mockSqsClient.Object, QueueName);

        _mockSqsClient
            .Setup(s => s.GetQueueUrlAsync(QueueName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = QueueUrl });

        _mockSqsClient
            .Setup(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiveMessageResponse { Messages = [_message] });
    }

    private static async Task<ReceiveMessageResponse> RespondWithLongPollDelay(
        List<Message> messages,
        CancellationToken cancellationToken
    )
    {
        await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        return new ReceiveMessageResponse { Messages = messages };
    }

    [Fact]
    public async Task ExecuteAsync_CallsReceiveMessagesWithTheCorrectRequest()
    {
        using var cts = new CancellationTokenSource();
        ReceiveMessageRequest? receiveMessageRequest = null;

        _mockSqsClient
            .Setup(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ReceiveMessageRequest, CancellationToken>((req, _) => receiveMessageRequest = req)
            .Returns(
                async (ReceiveMessageRequest _, CancellationToken cancellationToken) =>
                    await RespondWithLongPollDelay([_message], cancellationToken)
            );

        _consumer = new TestConsumer(_logger, _mockSqsClient.Object, QueueName);

        var runTask = _consumer.RunAsync(cts.Token);
        cts.CancelAfter(s_cancellationTokenCancelAfter);
        await runTask.WaitAsync(s_testTimeout, TestContext.Current.CancellationToken);

        receiveMessageRequest!.MaxNumberOfMessages.Should().Be(10);
        receiveMessageRequest!.MessageAttributeNames[0].Should().Be("All");
        receiveMessageRequest!.MessageSystemAttributeNames[0].Should().Be("All");
        receiveMessageRequest!.QueueUrl.Should().Be(QueueUrl);
        receiveMessageRequest!.VisibilityTimeout.Should().Be(60);
        receiveMessageRequest!.WaitTimeSeconds.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoMessagesReceived_ItTriesAgain()
    {
        using var cts = new CancellationTokenSource();

        _mockSqsClient
            .Setup(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .Returns(
                async (ReceiveMessageRequest _, CancellationToken cancellationToken) =>
                    await RespondWithLongPollDelay([], cancellationToken)
            );

        var processMessage = new Mock<Func<Message, CancellationToken, Task>>();

        _consumer = new TestConsumer(_logger, _mockSqsClient.Object, QueueName, processMessage.Object);

        var runTask = _consumer.RunAsync(cts.Token);
        cts.CancelAfter(s_cancellationTokenCancelAfter);
        await runTask.WaitAsync(s_testTimeout, TestContext.Current.CancellationToken);

        _mockSqsClient.Verify(
            s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(2)
        );
    }

    [Fact]
    public async Task ExecuteAsync_WhenMessageReceived_ProcessesAndDeletesMessage()
    {
        using var cts = new CancellationTokenSource();
        Message? message = null;

        _mockSqsClient
            .Setup(s => s.DeleteMessageAsync(QueueUrl, _message.ReceiptHandle, It.IsAny<CancellationToken>()))
            .Callback(cts.Cancel)
            .ReturnsAsync(new DeleteMessageResponse());

        _consumer = new TestConsumer(
            _logger,
            _mockSqsClient.Object,
            QueueName,
            (msg, _) =>
            {
                message = msg;
                return Task.CompletedTask;
            }
        );

        var runTask = _consumer.RunAsync(cts.Token);
        cts.CancelAfter(s_cancellationTokenCancelAfter);
        await runTask.WaitAsync(s_testTimeout, TestContext.Current.CancellationToken);

        Assert.Equal(_message.Body, message!.Body);

        _mockSqsClient.Verify(
            s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        _mockSqsClient.Verify(
            s => s.DeleteMessageAsync(QueueUrl, _message.ReceiptHandle, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ExecuteAsync_WhenMessageReceived_AndIsFailedToBeProcessed_ItDoesNotDeleteTheMessage()
    {
        using var cts = new CancellationTokenSource();

        _mockSqsClient
            .Setup(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .Returns(
                async (ReceiveMessageRequest _, CancellationToken cancellationToken) =>
                    await RespondWithLongPollDelay([_message], cancellationToken)
            );

        _consumer = new TestConsumer(
            _logger,
            _mockSqsClient.Object,
            QueueName,
            (_, _) => throw new Exception("Failed to process message")
        );

        var runTask = _consumer.RunAsync(cts.Token);
        cts.CancelAfter(s_cancellationTokenCancelAfter);
        await runTask.WaitAsync(s_testTimeout, TestContext.Current.CancellationToken);

        _mockSqsClient.Verify(
            s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce
        );
        _mockSqsClient.Verify(
            s => s.DeleteMessageAsync(QueueUrl, _message.ReceiptHandle, It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ExecuteAsync_WhenFailedToReceiveMessages_ItHandlesTheException_AndDoesNothing()
    {
        using var cts = new CancellationTokenSource();

        _mockSqsClient
            .Setup(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .Returns(
                async (ReceiveMessageRequest _, CancellationToken cancellationToken) =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    throw new RequestThrottledException("Request throttled");
                }
            );

        var processMessage = new Mock<Func<Message, CancellationToken, Task>>();

        _consumer = new TestConsumer(_logger, _mockSqsClient.Object, QueueName, processMessage.Object);

        var runTask = _consumer.RunAsync(cts.Token);
        cts.CancelAfter(s_cancellationTokenCancelAfter);
        await runTask.WaitAsync(s_testTimeout, TestContext.Current.CancellationToken);

        _mockSqsClient.Verify(
            s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce
        );
        processMessage.Verify(s => s.Invoke(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationRequestedDuringProcessing_DoesNotDeleteMessage()
    {
        using var cts = new CancellationTokenSource();

        _consumer = new TestConsumer(
            _logger,
            _mockSqsClient.Object,
            QueueName,
            (_, token) =>
            {
                cts.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        );

        var runTask = _consumer.RunAsync(cts.Token);
        await runTask.WaitAsync(s_testTimeout, TestContext.Current.CancellationToken);

        _mockSqsClient.Verify(
            s => s.DeleteMessageAsync(QueueUrl, _message.ReceiptHandle, It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    private sealed class TestConsumer(
        ILogger<TestConsumer> logger,
        IAmazonSQS sqsClient,
        string queueName,
        Func<Message, CancellationToken, Task>? onProcessMessage = null
    ) : SqsConsumer<TestConsumer>(logger, sqsClient, queueName)
    {
        private readonly Func<Message, CancellationToken, Task> _onProcessMessage =
            onProcessMessage ?? ((_, _) => Task.CompletedTask);

        protected override int WaitTimeSeconds => 1;

        protected override Task ProcessMessageAsync(Message message, CancellationToken stoppingToken)
        {
            return _onProcessMessage(message, stoppingToken);
        }

        public Task RunAsync(CancellationToken cancellationToken) => ExecuteAsync(cancellationToken);
    }
}
