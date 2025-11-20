using AutoFixture;
using GmrProcessor.Data;
using GmrProcessor.Processors;
using MongoDB.Driver;
using Moq;
using TestFixtures;

namespace GmrProcessor.Tests.Processors;

public class GtoImportPreNotificationProcessorTests
{
    private readonly Mock<IMongoContext> _mockMongoContext = new();
    private readonly Mock<IMongoCollectionSet<ImportTransit>> _mockImportTransits = new();
    private readonly GtoImportPreNotificationProcessor _processor;

    public GtoImportPreNotificationProcessorTests()
    {
        _mockMongoContext.Setup(x => x.ImportTransits).Returns(_mockImportTransits.Object);
        _processor = new GtoImportPreNotificationProcessor(_mockMongoContext.Object);
    }

    [Fact]
    public async Task ProcessAsync_InsertsImportTransitIfNotExists()
    {
        var resourceId = "CHEDD.GB.2024.1234567";

        var importPreNotification = ImportPreNotificationFixtures.ImportPreNotificationFixture().Create();
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
}
