using System.Linq.Expressions;
using AutoFixture;
using Defra.TradeImportsGmrFinder.Domain.Events;
using GmrProcessor.Data;
using GmrProcessor.Processors.Gto;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using NSubstitute.ReturnsExtensions;
using TestFixtures;

namespace GmrProcessor.Tests.Processors.Gto;

public class GtoMatchedGmrProcessorTests
{
    private readonly Mock<ILogger<GtoMatchedGmrProcessor>> _logger = new();
    private readonly Mock<IMongoContext> _mongoContext = new();
    private readonly Mock<IMatchedGmrItemCollection> _mockGtoMatchedGmrItemCollection = new();
    private readonly Mock<IMongoCollectionSet<ImportTransit>> _mockImportTransitsCollection = new();
    private readonly GtoMatchedGmrProcessor _processor;

    public GtoMatchedGmrProcessorTests()
    {
        _mongoContext.Setup(x => x.GtoMatchedGmrItem).Returns(_mockGtoMatchedGmrItemCollection.Object);
        _mongoContext.Setup(x => x.ImportTransits).Returns(_mockImportTransitsCollection.Object);
        _processor = new GtoMatchedGmrProcessor(_logger.Object, _mongoContext.Object);
    }

    [Fact]
    public async Task Process_WhenNoImportTransitFound_LogsAndSkipsProcessing()
    {
        var matchedGmr = new MatchedGmr
        {
            Mrn = CustomsDeclarationFixtures.GenerateMrn(),
            Gmr = GmrFixtures.GmrFixture().Create(),
        };

        _mockImportTransitsCollection
            .Setup(x => x.FindOne(It.IsAny<Expression<Func<ImportTransit, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ImportTransit?)null);

        await _processor.Process(matchedGmr, CancellationToken.None);

        _logger.Verify(
            x =>
                x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("No import transit found for Mrn")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );

        _mockGtoMatchedGmrItemCollection.Verify(
            x => x.UpdateOrInsert(It.IsAny<MatchedGmrItem>(), CancellationToken.None),
            Times.Never
        );
    }

    [Fact]
    public async Task Process_WhenUpdateResultIsNewOrUpdated_InsertsOrUpdatesAndLogs()
    {
        var expectedUpdatedTime = new DateTimeOffset(2025, 11, 24, 9, 0, 0, TimeSpan.Zero);
        var gmr = GmrFixtures.GmrFixture().WithDateTime(expectedUpdatedTime).Create();
        var matchedGmr = new MatchedGmr { Mrn = CustomsDeclarationFixtures.GenerateMrn(), Gmr = gmr };
        var importTransitId = ImportPreNotificationFixtures.GenerateRandomReference();

        _mockImportTransitsCollection
            .Setup(x => x.FindOne(It.IsAny<Expression<Func<ImportTransit, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ImportTransit
                {
                    Id = importTransitId,
                    Mrn = matchedGmr.Mrn,
                    TransitOverrideRequired = true,
                }
            );

        _mockGtoMatchedGmrItemCollection
            .Setup(x => x.UpdateOrInsert(It.IsAny<MatchedGmrItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, BsonValue.Create("id")));

        await _processor.Process(matchedGmr, CancellationToken.None);

        _mockGtoMatchedGmrItemCollection.Verify(
            x =>
                x.UpdateOrInsert(
                    It.Is<MatchedGmrItem>(item =>
                        item.ImportTransitId == importTransitId
                        && item.Mrn == matchedGmr.Mrn
                        && item.GmrId == gmr.GmrId
                        && item.Gmr == gmr
                        && item.UpdatedDateTime == expectedUpdatedTime.UtcDateTime
                    ),
                    CancellationToken.None
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Process_WhenUpdateResultIsNotNew_LogsThatItWasSkipped()
    {
        var matchedGmr = new MatchedGmr
        {
            Mrn = CustomsDeclarationFixtures.GenerateMrn(),
            Gmr = GmrFixtures.GmrFixture().Create(),
        };
        var importTransitId = ImportPreNotificationFixtures.GenerateRandomReference();

        _mockImportTransitsCollection
            .Setup(x => x.FindOne(It.IsAny<Expression<Func<ImportTransit, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ImportTransit
                {
                    Id = importTransitId,
                    Mrn = matchedGmr.Mrn,
                    TransitOverrideRequired = true,
                }
            );

        _mockGtoMatchedGmrItemCollection
            .Setup(x => x.UpdateOrInsert(It.IsAny<MatchedGmrItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(1, 0, null));

        await _processor.Process(matchedGmr, CancellationToken.None);

        _logger.Verify(
            x =>
                x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Skipping an old GMR item")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );

        _logger.Verify(
            x =>
                x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("New Matched GMR item inserted")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Never
        );
    }
}
