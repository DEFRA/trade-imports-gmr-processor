using Azure.Messaging.ServiceBus;
using GmrProcessor.Services;
using Microsoft.Extensions.Azure;
using Moq;

namespace GmrProcessor.Tests.Services;

public class TradeImportsTradeImportsServiceBusTests
{
    [Fact]
    public async Task SendMessagesAsync_CreatesClientForCorrectQueue()
    {
        var mockSender = new Mock<ServiceBusSender>();
        var mockFactory = new Mock<IAzureClientFactory<ServiceBusSender>>();

        var messages = new[] { new { Id = 1 }, new { Id = 2 }, new { Id = 3 } };
        const string queueName = "test-queue";

        mockFactory.Setup(f => f.CreateClient(queueName)).Returns(mockSender.Object);

        var service = new TradeImportsTradeImportsServiceBus(mockFactory.Object);

        try
        {
            await service.SendMessagesAsync(messages, queueName, CancellationToken.None);
        }
        catch
        {
            // Expected to fail when trying to create the actual batch
        }

        mockFactory.Verify(f => f.CreateClient(queueName), Times.Once);
    }
}
