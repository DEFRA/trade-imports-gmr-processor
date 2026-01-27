using AutoFixture;
using Defra.TradeImportsGmrFinder.Domain.Events;
using GmrProcessor.Data;
using GmrProcessor.Data.Common;
using GmrProcessor.Data.Gto;
using GmrProcessor.Extensions;
using GmrProcessor.Processors.Gto;
using GmrProcessor.Services;
using Microsoft.Extensions.Logging;
using Moq;
using TestFixtures;

namespace GmrProcessor.Tests.Processors.Gto;

public class GtoMatchedGmrProcessorTests
{
    private readonly Mock<ILogger<GtoMatchedGmrProcessor>> _logger = new();
    private readonly Mock<IGtoMatchedGmrCollection> _mockMatchedGmrRepository = new();
    private readonly Mock<IGtoGmrCollection> _mockGtoGmrCollection = new();
    private readonly Mock<IGtoImportTransitCollection> _mockImportTransitRepository = new();
    private readonly Mock<IGvmsHoldService> _gvmsHoldService = new();
    private readonly GtoMatchedGmrProcessor _processor;

    public GtoMatchedGmrProcessorTests()
    {
        _processor = new GtoMatchedGmrProcessor(
            _logger.Object,
            _mockMatchedGmrRepository.Object,
            _mockGtoGmrCollection.Object,
            _mockImportTransitRepository.Object,
            _gvmsHoldService.Object
        );
    }

    [Fact]
    public async Task Process_WhenNoImportTransitFound_ReturnsSkippedNoTransit()
    {
        var matched = BuildMatchedGmr();
        _mockImportTransitRepository
            .Setup(r => r.GetByMrn(matched.Mrn!, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ImportTransit?)null);

        var result = await _processor.Process(matched, CancellationToken.None);

        result.Should().Be(GtoMatchedGmrProcessorResult.SkippedNoTransit);
        _mockMatchedGmrRepository.Verify(
            r => r.UpsertMatchedItem(It.IsAny<MatchedGmr>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Process_WhenNoImportTransitFound_LogsSkippingMessage()
    {
        var matched = BuildMatchedGmr();
        _mockImportTransitRepository
            .Setup(r => r.GetByMrn(matched.Mrn!, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ImportTransit?)null);

        await _processor.Process(matched, CancellationToken.None);

        _logger.Verify(
            l =>
                l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>(
                        (state, _) =>
                            state.ToString() == $"GTO: Skipping {matched.Mrn} because no import transit was found"
                    ),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Process_WhenImportTransitIsFound_PassesToGvmsHoldService()
    {
        var matched = BuildMatchedGmr();
        var gtoGmr = BuildGtoGmr(matched.Gmr, holdStatus: false);
        var importTransit = new ImportTransit
        {
            Id = ImportPreNotificationFixtures.GenerateRandomReference(),
            Mrn = matched.Mrn,
            TransitOverrideRequired = true,
        };

        _mockImportTransitRepository
            .Setup(r => r.GetByMrn(matched.Mrn!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(importTransit);
        _mockMatchedGmrRepository
            .Setup(r => r.UpsertMatchedItem(It.IsAny<MatchedGmr>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockGtoGmrCollection
            .Setup(r => r.UpdateOrInsert(It.IsAny<GtoGmr>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gtoGmr);
        _gvmsHoldService
            .Setup(x => x.PlaceOrReleaseHold(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GvmsHoldResult.HoldPlaced);

        var result = await _processor.Process(matched, CancellationToken.None);

        result.Should().Be(GtoMatchedGmrProcessorResult.HoldPlaced);
        _gvmsHoldService.Verify(
            g => g.PlaceOrReleaseHold(matched.Gmr.GmrId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Process_WhenIncomingGmrIsOld_ReturnsSkippedOldGmr()
    {
        var matched = BuildMatchedGmr();
        var existing = BuildGtoGmr(
            matched.Gmr,
            holdStatus: false,
            updatedOverride: matched.Gmr.GetUpdatedDateTime().AddMinutes(5)
        );
        var importTransit = new ImportTransit
        {
            Id = ImportPreNotificationFixtures.GenerateRandomReference(),
            Mrn = matched.Mrn,
            TransitOverrideRequired = false,
        };

        _mockImportTransitRepository
            .Setup(r => r.GetByMrn(matched.Mrn!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(importTransit);
        _mockGtoGmrCollection
            .Setup(r => r.UpdateOrInsert(It.IsAny<GtoGmr>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await _processor.Process(matched, CancellationToken.None);

        result.Should().Be(GtoMatchedGmrProcessorResult.SkippedOldGmr);
        _gvmsHoldService.Verify(
            g => g.PlaceOrReleaseHold(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Process_WhenIncomingGmrIsOld_LogsSkippingOldGmrMessage()
    {
        var matched = BuildMatchedGmr();
        var existing = BuildGtoGmr(
            matched.Gmr,
            holdStatus: false,
            updatedOverride: matched.Gmr.GetUpdatedDateTime().AddMinutes(5)
        );
        var importTransit = new ImportTransit
        {
            Id = ImportPreNotificationFixtures.GenerateRandomReference(),
            Mrn = matched.Mrn,
            TransitOverrideRequired = false,
        };

        _mockImportTransitRepository
            .Setup(r => r.GetByMrn(matched.Mrn!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(importTransit);
        _mockGtoGmrCollection
            .Setup(r => r.UpdateOrInsert(It.IsAny<GtoGmr>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await _processor.Process(matched, CancellationToken.None);

        _logger.Verify(
            l =>
                l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>(
                        (state, _) =>
                            state.ToString()
                            == $"GTO: Skipping {matched.Mrn} because it is an old GTO GMR item, Gmr: {matched.Gmr.GmrId}, UpdatedTime: {matched.Gmr.UpdatedDateTime}"
                    ),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Process_WhenGmrUpserted_LogsMatchedInsertedUpdatedMessage()
    {
        var matched = BuildMatchedGmr();
        var gtoGmr = BuildGtoGmr(matched.Gmr, holdStatus: false);
        var importTransit = new ImportTransit
        {
            Id = ImportPreNotificationFixtures.GenerateRandomReference(),
            Mrn = matched.Mrn,
            TransitOverrideRequired = true,
        };

        _mockImportTransitRepository
            .Setup(r => r.GetByMrn(matched.Mrn!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(importTransit);
        _mockGtoGmrCollection
            .Setup(r => r.UpdateOrInsert(It.IsAny<GtoGmr>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gtoGmr);
        _mockMatchedGmrRepository
            .Setup(r => r.UpsertMatchedItem(matched, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _processor.Process(matched, CancellationToken.None);

        _logger.Verify(
            l =>
                l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>(
                        (state, _) =>
                            state.ToString()
                            == $"GTO: Matched GMR item inserted/updated, Mrn: {matched.Mrn}, Gmr: {matched.Gmr.GmrId}, UpdatedTime: {matched.Gmr.UpdatedDateTime}"
                    ),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }

    private static MatchedGmr BuildMatchedGmr()
    {
        var updated = new DateTimeOffset(2025, 11, 24, 9, 0, 0, TimeSpan.Zero);
        return new MatchedGmr
        {
            Mrn = CustomsDeclarationFixtures.GenerateMrn(),
            Gmr = GmrFixtures.GmrFixture().WithDateTime(updated).Create(),
        };
    }

    private static GtoGmr BuildGtoGmr(
        Defra.TradeImportsGmrFinder.GvmsClient.Contract.Gmr gmr,
        bool holdStatus,
        DateTime? updatedOverride = null
    ) =>
        new()
        {
            Id = gmr.GmrId,
            Gmr = gmr,
            HoldStatus = holdStatus,
            UpdatedDateTime = updatedOverride ?? gmr.GetUpdatedDateTime(),
        };
}
