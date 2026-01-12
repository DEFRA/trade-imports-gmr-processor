using System.Text.Json;
using GmrProcessor.Data;
using GmrProcessor.Services;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;

namespace GmrProcessor.Tests.Services;

public class TradeImportsServiceBusWithStorageTests
{
    private readonly Mock<ITradeImportsServiceBus> _serviceBus = new();
    private readonly Mock<IMongoContext> _mongoContext = new();
    private readonly Mock<IMongoCollectionSet<MessageAudit>> _messageAudits = new();
    private readonly Mock<ILogger<TradeImportsServiceBusWithStorage>> _logger = new();
    private readonly TradeImportsServiceBusWithStorage _service;

    public TradeImportsServiceBusWithStorageTests()
    {
        _mongoContext.Setup(m => m.MessageAudits).Returns(_messageAudits.Object);
        _service = new TradeImportsServiceBusWithStorage(_serviceBus.Object, _mongoContext.Object, _logger.Object);
    }

    [Fact]
    public async Task SendMessagesAsync_SendsMessagesFirst()
    {
        var messages = new[] { new { Id = 1 }, new { Id = 2 } };
        const string queueName = "test-queue";

        _serviceBus
            .Setup(s => s.SendMessagesAsync(It.IsAny<IEnumerable<object>>(), queueName, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _messageAudits
            .Setup(m => m.BulkWrite(It.IsAny<List<WriteModel<MessageAudit>>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.SendMessagesAsync(messages, queueName, CancellationToken.None);

        _serviceBus.Verify(
            s => s.SendMessagesAsync(It.IsAny<IEnumerable<object>>(), queueName, CancellationToken.None),
            Times.Once
        );
    }

    [Fact]
    public async Task SendMessagesAsync_StoresMessagesInMongoDB()
    {
        var messages = new[] { new { Id = 1, Name = "Test" }, new { Id = 2, Name = "Test2" } };
        const string queueName = "test-queue";

        _serviceBus
            .Setup(s => s.SendMessagesAsync(It.IsAny<IEnumerable<object>>(), queueName, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        List<WriteModel<MessageAudit>>? capturedModels = null;

        _messageAudits
            .Setup(m => m.BulkWrite(It.IsAny<List<WriteModel<MessageAudit>>>(), It.IsAny<CancellationToken>()))
            .Callback(
                (List<WriteModel<MessageAudit>> models, CancellationToken _) =>
                {
                    capturedModels = models;
                }
            )
            .Returns(Task.CompletedTask);

        await _service.SendMessagesAsync(messages, queueName, CancellationToken.None);

        Assert.NotNull(capturedModels);
        Assert.Equal(2, capturedModels.Count);
    }

    [Fact]
    public async Task SendMessagesAsync_DoesNotThrowOnStorageFailure()
    {
        var messages = new[] { new { Id = 1 } };
        const string queueName = "test-queue";

        _serviceBus
            .Setup(s => s.SendMessagesAsync(It.IsAny<IEnumerable<object>>(), queueName, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _messageAudits
            .Setup(m => m.BulkWrite(It.IsAny<List<WriteModel<MessageAudit>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("MongoDB connection failed"));

        await _service.SendMessagesAsync(messages, queueName, CancellationToken.None);

        _serviceBus.Verify(
            s => s.SendMessagesAsync(It.IsAny<IEnumerable<object>>(), queueName, CancellationToken.None),
            Times.Once
        );
    }
}
