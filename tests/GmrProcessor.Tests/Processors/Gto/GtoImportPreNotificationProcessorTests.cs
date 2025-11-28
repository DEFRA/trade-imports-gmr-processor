using AutoFixture;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using GmrProcessor.Data;
using GmrProcessor.Processors.Gto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Moq;
using TestFixtures;

namespace GmrProcessor.Tests.Processors.Gto;

public class GtoImportPreNotificationProcessorTests
{
    private readonly Mock<IMongoContext> _mockMongoContext = new();
    private readonly Mock<IMongoCollectionSet<ImportTransit>> _mockImportTransits = new();
    private readonly GtoImportPreNotificationProcessor _processor;

    public GtoImportPreNotificationProcessorTests()
    {
        _mockMongoContext.Setup(x => x.ImportTransits).Returns(_mockImportTransits.Object);
        _processor = new GtoImportPreNotificationProcessor(
            _mockMongoContext.Object,
            NullLogger<GtoImportPreNotificationProcessor>.Instance
        );
    }

    [Fact]
    public async Task ProcessAsync_InsertsImportTransitIfNotExists()
    {
        var resourceId = "CHEDD.GB.2024.1234567";

        var importPreNotification = ImportPreNotificationFixtures
            .ImportPreNotificationFixture()
            .With(x => x.PartOne, new PartOne { ProvideCtcMrn = "YES" })
            .With(
                x => x.ExternalReferences,
                [new ExternalReference { System = "NCTS", Reference = "24GB12345678901234" }]
            )
            .Create();
        var resourceEvent = ImportPreNotificationFixtures
            .ImportPreNotificationResourceEventFixture(importPreNotification)
            .With(r => r.ResourceId, resourceId)
            .Create();

        await _processor.ProcessAsync(resourceEvent, CancellationToken.None);

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
        var resourceId = "CHEDD.GB.2024.1234567";

        var importPreNotification = ImportPreNotificationFixtures
            .ImportPreNotificationFixture()
            .With(x => x.PartOne, new PartOne { ProvideCtcMrn = "NO" })
            .Create();
        var resourceEvent = ImportPreNotificationFixtures
            .ImportPreNotificationResourceEventFixture(importPreNotification)
            .With(r => r.ResourceId, resourceId)
            .Create();

        await _processor.ProcessAsync(resourceEvent, CancellationToken.None);

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
}
