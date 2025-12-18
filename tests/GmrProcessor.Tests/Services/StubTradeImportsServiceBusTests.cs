using System.Text.Json;
using GmrProcessor.Domain.Eta;
using GmrProcessor.Services;
using Microsoft.Extensions.Logging;
using Moq;
using TestFixtures;

namespace GmrProcessor.Tests.Services;

public class StubTradeImportsServiceBusTests
{
    [Fact]
    public async Task SendMessagesAsync_LogsExpectedMessage()
    {
        var logger = new Mock<ILogger<StubTradeImportsServiceBus>>();
        var serviceBus = new StubTradeImportsServiceBus(logger.Object);

        const string queueName = "eta-queue";
        var messages = new List<IpaffsUpdatedTimeOfArrivalMessage>
        {
            new()
            {
                ReferenceNumber = ImportPreNotificationFixtures.GenerateRandomReference(),
                Mrn = CustomsDeclarationFixtures.GenerateMrn(),
                LocalDateTimeOfArrival = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            },
            new()
            {
                ReferenceNumber = ImportPreNotificationFixtures.GenerateRandomReference(),
                Mrn = CustomsDeclarationFixtures.GenerateMrn(),
                LocalDateTimeOfArrival = new DateTime(2025, 1, 2, 4, 5, 6, DateTimeKind.Utc),
            },
        };

        var expectedJson = JsonSerializer.Serialize(messages.AsEnumerable());
        var expectedLogMessage = $"Would send the following to {queueName}: {expectedJson}";

        await serviceBus.SendMessagesAsync(messages, queueName, CancellationToken.None);

        logger.Verify(
            l =>
                l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString() == expectedLogMessage),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }
}
