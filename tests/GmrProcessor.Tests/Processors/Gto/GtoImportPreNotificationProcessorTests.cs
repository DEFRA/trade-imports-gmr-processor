using Defra.TradeImportsDataApi.Domain.Ipaffs;
using GmrProcessor.Data;
using GmrProcessor.Data.Gto;
using GmrProcessor.Processors.Gto;
using GmrProcessor.Services;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Moq;
using TestFixtures;

namespace GmrProcessor.Tests.Processors.Gto;

public class GtoImportPreNotificationProcessorTests
{
    private readonly Mock<IMongoContext> _mockMongoContext = new();
    private readonly Mock<IMongoCollectionSet<ImportTransit>> _mockImportTransits = new();
    private readonly Mock<IGtoMatchedGmrCollection> _mockGtoMatchedGmrRepository = new();
    private readonly Mock<IGvmsHoldService> _mockGvmsHoldService = new();
    private readonly GtoImportPreNotificationProcessor _processor;

    public GtoImportPreNotificationProcessorTests()
    {
        _mockMongoContext.Setup(x => x.ImportTransits).Returns(_mockImportTransits.Object);
        _processor = new GtoImportPreNotificationProcessor(
            _mockMongoContext.Object,
            NullLogger<GtoImportPreNotificationProcessor>.Instance,
            _mockGtoMatchedGmrRepository.Object,
            _mockGvmsHoldService.Object
        );
    }

    [Fact]
    public async Task ProcessAsync_InsertsImportTransitIfNotExists()
    {
        var importPreNotification = ImportPreNotificationFixtures
            .ImportPreNotificationFixture("CHEDD.GB.2024.1234567")
            .With(x => x.PartOne, new PartOne { ProvideCtcMrn = "YES" })
            .With(
                x => x.ExternalReferences,
                [new ExternalReference { System = "NCTS", Reference = "24GB12345678901234" }]
            )
            .Create();
        var resourceEvent = ImportPreNotificationFixtures
            .ImportPreNotificationResourceEventFixture(importPreNotification)
            .Create();

        await _processor.Process(resourceEvent, CancellationToken.None);

        _mockImportTransits.Verify(
            x =>
                x.FindOneAndUpdate(
                    It.IsAny<FilterDefinition<ImportTransit>>(),
                    It.IsAny<UpdateDefinition<ImportTransit>>(),
                    It.Is<FindOneAndUpdateOptions<ImportTransit>>(o => o.IsUpsert),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task ProcessAsync_DoesNotInsertImportTransitWhenItsNotATransit()
    {
        var importPreNotification = ImportPreNotificationFixtures
            .ImportPreNotificationFixture("CHEDD.GB.2024.1234567")
            .With(x => x.PartOne, new PartOne { ProvideCtcMrn = "NO" })
            .Create();
        var resourceEvent = ImportPreNotificationFixtures
            .ImportPreNotificationResourceEventFixture(importPreNotification)
            .Create();

        await _processor.Process(resourceEvent, CancellationToken.None);

        _mockImportTransits.Verify(
            x =>
                x.FindOneAndUpdate(
                    It.IsAny<FilterDefinition<ImportTransit>>(),
                    It.IsAny<UpdateDefinition<ImportTransit>>(),
                    It.Is<FindOneAndUpdateOptions<ImportTransit>>(o => o.IsUpsert),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task ProcessAsync_WhenHoldChange_DoesNotCallGvms()
    {
        const string resourceId = "CHEDD.GB.2024.1234567";
        const string mrn = "24GB12345678901234";

        var importPreNotification = ImportPreNotificationFixtures
            .ImportPreNotificationFixture(resourceId)
            .With(x => x.PartOne, new PartOne { ProvideCtcMrn = "YES" })
            .With(x => x.ExternalReferences, [new ExternalReference { System = "NCTS", Reference = mrn }])
            .With(x => x.PartTwo, new PartTwo { InspectionRequired = "Required" })
            .With(x => x.Status, string.Empty)
            .Create();
        var resourceEvent = ImportPreNotificationFixtures
            .ImportPreNotificationResourceEventFixture(importPreNotification)
            .Create();

        _mockImportTransits
            .Setup(x =>
                x.FindOneAndUpdate(
                    It.IsAny<FilterDefinition<ImportTransit>>(),
                    It.IsAny<UpdateDefinition<ImportTransit>>(),
                    It.IsAny<FindOneAndUpdateOptions<ImportTransit>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ImportTransit
                {
                    Id = resourceId,
                    TransitOverrideRequired = false,
                    Mrn = mrn,
                }
            );
        _mockGtoMatchedGmrRepository
            .Setup(x => x.GetByMrn(mrn, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MatchedGmrItem?)null);

        await _processor.Process(resourceEvent, CancellationToken.None);

        _mockGtoMatchedGmrRepository.Verify(x => x.GetByMrn(mrn, It.IsAny<CancellationToken>()), Times.Once);
        _mockGvmsHoldService.Verify(
            x => x.PlaceOrReleaseHold(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ProcessAsync_WhenMatchedGmrExists_PassesToGvmsHoldService()
    {
        const string importReference = "CHEDD.GB.2024.1234567";
        const string mrn = "24GB12345678901234";
        var gmrId = GmrFixtures.GenerateGmrId();

        var importPreNotification = ImportPreNotificationFixtures
            .ImportPreNotificationFixture(importReference)
            .With(x => x.PartOne, new PartOne { ProvideCtcMrn = "YES" })
            .With(x => x.ExternalReferences, [new ExternalReference { System = "NCTS", Reference = mrn }])
            .With(x => x.PartTwo, new PartTwo { InspectionRequired = "Required" })
            .With(x => x.Status, string.Empty)
            .Create();
        var resourceEvent = ImportPreNotificationFixtures
            .ImportPreNotificationResourceEventFixture(importPreNotification)
            .Create();

        _mockImportTransits
            .Setup(x =>
                x.FindOneAndUpdate(
                    It.IsAny<FilterDefinition<ImportTransit>>(),
                    It.IsAny<UpdateDefinition<ImportTransit>>(),
                    It.IsAny<FindOneAndUpdateOptions<ImportTransit>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ImportTransit
                {
                    Id = importReference,
                    TransitOverrideRequired = false,
                    Mrn = mrn,
                }
            );
        _mockGtoMatchedGmrRepository
            .Setup(x => x.GetByMrn(mrn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MatchedGmrItem { Mrn = mrn, GmrId = gmrId });

        await _processor.Process(resourceEvent, CancellationToken.None);

        _mockGtoMatchedGmrRepository.Verify(x => x.GetByMrn(mrn, It.IsAny<CancellationToken>()), Times.Once);
        _mockGvmsHoldService.Verify(x => x.PlaceOrReleaseHold(gmrId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
