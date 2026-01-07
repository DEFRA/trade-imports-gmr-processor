using Defra.TradeImportsGmrFinder.GvmsClient.Client;
using GmrProcessor.Data;
using GmrProcessor.Data.Gto;
using GmrProcessor.Services;
using Microsoft.Extensions.Logging;
using Moq;
using TestFixtures;

namespace GmrProcessor.Tests.Services;

public class GvmsApiClientServiceTests
{
    private readonly Mock<IGvmsApiClient> _gvmsApiClient = new();
    private readonly Mock<ILogger<GvmsApiClientService>> _logger = new();
    private readonly Mock<IMongoContext> _mongoContext = new();
    private readonly Mock<IGtoGmrCollection> _gtoGmrCollection = new();
    private readonly GvmsApiClientService _service;

    public GvmsApiClientServiceTests()
    {
        _mongoContext.Setup(m => m.GtoGmr).Returns(_gtoGmrCollection.Object);
        _service = new GvmsApiClientService(_logger.Object, _gvmsApiClient.Object, _mongoContext.Object);
    }

    [Fact]
    public async Task PlaceOrReleaseHold_CallsClientAndUpdatesHoldStatusInDatabase()
    {
        var gmrId = GmrFixtures.GenerateGmrId();
        const bool hold = true;

        _gvmsApiClient.Setup(c => c.HoldGmr(gmrId, hold, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _gtoGmrCollection
            .Setup(c => c.UpdateHoldStatus(gmrId, hold, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.PlaceOrReleaseHold(gmrId, hold, CancellationToken.None);

        _gvmsApiClient.Verify(c => c.HoldGmr(gmrId, hold, CancellationToken.None), Times.Once);
        _gtoGmrCollection.Verify(c => c.UpdateHoldStatus(gmrId, hold, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task PlaceOrReleaseHold_LogsActionMessage()
    {
        var gmrId = GmrFixtures.GenerateGmrId();
        const bool hold = true;

        _gvmsApiClient.Setup(c => c.HoldGmr(gmrId, hold, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _gtoGmrCollection
            .Setup(c => c.UpdateHoldStatus(gmrId, hold, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.PlaceOrReleaseHold(gmrId, hold, CancellationToken.None);

        _logger.Verify(
            l =>
                l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString() == $"Placing GVMS hold on {gmrId}"),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }
}
