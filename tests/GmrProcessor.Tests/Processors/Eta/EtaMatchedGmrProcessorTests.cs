using Defra.TradeImportsDataApi.Api.Client;
using Defra.TradeImportsGmrFinder.Domain.Events;
using GmrProcessor.Data;
using GmrProcessor.Data.Eta;
using GmrProcessor.Extensions;
using GmrProcessor.Processors.Eta;
using GmrProcessor.Processors.Gto;
using Microsoft.Extensions.Logging;
using Moq;
using TestFixtures;

namespace GmrProcessor.Tests.Processors.Eta;

public class EtaMatchedGmrProcessorTests
{
    private readonly Mock<ILogger<EtaMatchedGmrProcessor>> _logger = new();
    private readonly Mock<IGtoImportTransitRepository> _importTransitRepository = new();
    private readonly Mock<ITradeImportsDataApiClient> _tradeImportsDataApi = new();
    private readonly Mock<IEtaGmrCollection> _etaGmrCollection = new();
    private readonly EtaMatchedGmrProcessor _processor;

    public EtaMatchedGmrProcessorTests()
    {
        _processor = new EtaMatchedGmrProcessor(
            _logger.Object,
            _importTransitRepository.Object,
            _tradeImportsDataApi.Object,
            _etaGmrCollection.Object
        );
    }

    [Fact]
    public async Task Process_WhenGmrNotEmbarked_ReturnsSkippedNotEmbarked()
    {
        var matched = BuildMatchedGmr(state: "OPEN");

        var result = await _processor.Process(matched, CancellationToken.None);

        result.Should().Be(EtaMatchedGmrProcessorResult.SkippedNotEmbarked);
        _etaGmrCollection.Verify(c => c.UpdateOrInsert(It.IsAny<EtaGmr>(), It.IsAny<CancellationToken>()), Times.Never);
        _tradeImportsDataApi.VerifyNoOtherCalls();
        _importTransitRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Process_WhenIncomingCheckedInTimeIsOlder_ReturnsSkippedOldGmr()
    {
        var newGmrCheckedInCrossing = DateTimeOffset.UtcNow;
        var existingCheckedInCrossing = BuildNewerMatchedGmr();

        var newGmr = BuildMatchedGmr("EMBARKED", newGmrCheckedInCrossing);
        var existingEtaGmr = new EtaGmr
        {
            Id = existingCheckedInCrossing.Gmr.GmrId,
            Gmr = existingCheckedInCrossing.Gmr,
            UpdatedDateTime = existingCheckedInCrossing.Gmr.GetUpdatedDateTime(),
        };

        _etaGmrCollection
            .Setup(c => c.UpdateOrInsert(It.IsAny<EtaGmr>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEtaGmr);

        var result = await _processor.Process(newGmr, CancellationToken.None);

        result.Should().Be(EtaMatchedGmrProcessorResult.SkippedOldGmr);
        _tradeImportsDataApi.Verify(
            api => api.GetImportPreNotificationsByMrn(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        _importTransitRepository.Verify(
            r => r.GetByMrns(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Process_WhenCheckedInTimeUnchanged_ReturnsSkippedOldGmr()
    {
        var matched = BuildMatchedGmr();
        var existingEtaGmr = new EtaGmr
        {
            Id = matched.Gmr.GmrId,
            Gmr = matched.Gmr,
            UpdatedDateTime = matched.Gmr.GetUpdatedDateTime(),
        };

        _etaGmrCollection
            .Setup(c => c.UpdateOrInsert(It.IsAny<EtaGmr>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEtaGmr);

        var result = await _processor.Process(matched, CancellationToken.None);

        result.Should().Be(EtaMatchedGmrProcessorResult.SkippedOldGmr);
        _tradeImportsDataApi.VerifyNoOtherCalls();
        _importTransitRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Process_WhenNoChedsAssociatedWithGmrFound_ReturnsNoChedsFound()
    {
        var matched = BuildMatchedGmr();
        var existingGmr = BuildOlderMatchedGmr();

        var updatedDateTime = matched.Gmr.GetUpdatedDateTime();
        _etaGmrCollection
            .Setup(c => c.UpdateOrInsert(It.IsAny<EtaGmr>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new EtaGmr
                {
                    Id = existingGmr.Gmr.GmrId,
                    Gmr = existingGmr.Gmr,
                    UpdatedDateTime = updatedDateTime,
                }
            );

        _tradeImportsDataApi
            .Setup(api => api.GetImportPreNotificationsByMrn(matched.Mrn!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportPreNotificationsResponse([]));
        _importTransitRepository
            .Setup(r => r.GetByMrns(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _processor.Process(matched, CancellationToken.None);

        result.Should().Be(EtaMatchedGmrProcessorResult.NoChedsFound);
    }

    [Fact]
    public async Task Process_WhenTransitOnlyChedsFound_ReturnsUpdatedIpaffs()
    {
        var matched = BuildMatchedGmr();
        var existingGmr = BuildOlderMatchedGmr();
        var transitId = ImportPreNotificationFixtures.GenerateRandomReference();

        _etaGmrCollection
            .Setup(c => c.UpdateOrInsert(It.IsAny<EtaGmr>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new EtaGmr
                {
                    Id = existingGmr.Gmr.GmrId,
                    Gmr = existingGmr.Gmr,
                    UpdatedDateTime = existingGmr.Gmr.GetUpdatedDateTime(),
                }
            );

        _tradeImportsDataApi
            .Setup(api => api.GetImportPreNotificationsByMrn(matched.Mrn!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportPreNotificationsResponse([]));

        _importTransitRepository
            .Setup(r => r.GetByMrns(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ImportTransit
                {
                    Id = transitId,
                    Mrn = matched.Mrn,
                    TransitOverrideRequired = false,
                },
            ]);

        var result = await _processor.Process(matched, CancellationToken.None);

        result.Should().Be(EtaMatchedGmrProcessorResult.UpdatedIpaffs);
    }

    [Fact]
    public async Task Process_WhenImportOnlyChedsFound_ReturnsUpdatedIpaffs()
    {
        var matched = BuildMatchedGmr();
        var existingGmr = BuildOlderMatchedGmr();
        var importRef = ImportPreNotificationFixtures.GenerateRandomReference();

        _etaGmrCollection
            .Setup(c => c.UpdateOrInsert(It.IsAny<EtaGmr>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new EtaGmr
                {
                    Id = existingGmr.Gmr.GmrId,
                    Gmr = existingGmr.Gmr,
                    UpdatedDateTime = existingGmr.Gmr.GetUpdatedDateTime(),
                }
            );

        var apiResponse = new ImportPreNotificationsResponse([
            ImportPreNotificationFixtures
                .ImportPreNotificationResponseFixture(
                    ImportPreNotificationFixtures.ImportPreNotificationFixture(importRef).Create()
                )
                .Create(),
        ]);

        _tradeImportsDataApi
            .Setup(api => api.GetImportPreNotificationsByMrn(matched.Mrn!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        _importTransitRepository
            .Setup(r => r.GetByMrns(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _processor.Process(matched, CancellationToken.None);

        result.Should().Be(EtaMatchedGmrProcessorResult.UpdatedIpaffs);
    }

    [Fact]
    public async Task Process_WhenCheckedInTimeUpdated_AndChedsFound_UpdatesIpaffsWithLatestTime()
    {
        var matched = BuildNewerMatchedGmr();
        var existingGmr = BuildOlderMatchedGmr();

        var importReference = ImportPreNotificationFixtures.GenerateRandomReference();
        var transitId = ImportPreNotificationFixtures.GenerateRandomReference();

        _etaGmrCollection
            .Setup(c => c.UpdateOrInsert(It.IsAny<EtaGmr>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new EtaGmr
                {
                    Id = existingGmr.Gmr.GmrId,
                    Gmr = existingGmr.Gmr,
                    UpdatedDateTime = existingGmr.Gmr.GetUpdatedDateTime(),
                }
            );

        var apiResponse = new ImportPreNotificationsResponse([
            ImportPreNotificationFixtures
                .ImportPreNotificationResponseFixture(
                    ImportPreNotificationFixtures.ImportPreNotificationFixture(importReference).Create()
                )
                .Create(),
        ]);

        _tradeImportsDataApi
            .Setup(api => api.GetImportPreNotificationsByMrn(matched.Mrn!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        _importTransitRepository
            .Setup(r => r.GetByMrns(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ImportTransit
                {
                    Id = transitId,
                    Mrn = matched.Mrn,
                    TransitOverrideRequired = false,
                },
            ]);

        var result = await _processor.Process(matched, CancellationToken.None);

        result.Should().Be(EtaMatchedGmrProcessorResult.UpdatedIpaffs);
    }

    [Fact]
    public async Task Process_WhenNoExistingEtaRecord_StillProcessesAndReturnsUpdatedIpaffs()
    {
        var matched = BuildMatchedGmr();
        var importRef = ImportPreNotificationFixtures.GenerateRandomReference();

        _etaGmrCollection
            .Setup(c => c.UpdateOrInsert(It.IsAny<EtaGmr>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new EtaGmr
                {
                    Id = matched.Gmr.GmrId,
                    Gmr = null!,
                    UpdatedDateTime = matched.Gmr.GetUpdatedDateTime(),
                }
            );

        var apiResponse = new ImportPreNotificationsResponse([
            ImportPreNotificationFixtures
                .ImportPreNotificationResponseFixture(
                    ImportPreNotificationFixtures.ImportPreNotificationFixture(importRef).Create()
                )
                .Create(),
        ]);

        _tradeImportsDataApi
            .Setup(api => api.GetImportPreNotificationsByMrn(matched.Mrn!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        _importTransitRepository
            .Setup(r => r.GetByMrns(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _processor.Process(matched, CancellationToken.None);

        result.Should().Be(EtaMatchedGmrProcessorResult.UpdatedIpaffs);
    }

    private static MatchedGmr BuildMatchedGmr(
        string state = "EMBARKED",
        DateTimeOffset? checkedInCrossingDateTime = null
    )
    {
        var updated = DateTimeOffset.UtcNow;
        checkedInCrossingDateTime ??= DateTimeOffset.UtcNow;

        return new MatchedGmr
        {
            Mrn = CustomsDeclarationFixtures.GenerateMrn(),
            Gmr = GmrFixtures
                .GmrFixture()
                .WithDateTime(updated)
                .WithCheckedInCrossingDateTime(checkedInCrossingDateTime.Value)
                .With(g => g.State, state)
                .Create(),
        };
    }

    private static MatchedGmr BuildNewerMatchedGmr()
    {
        return BuildMatchedGmr(checkedInCrossingDateTime: DateTimeOffset.UtcNow.AddMinutes(2));
    }

    private static MatchedGmr BuildOlderMatchedGmr()
    {
        return BuildMatchedGmr(checkedInCrossingDateTime: DateTimeOffset.UtcNow.AddHours(-10));
    }
}
