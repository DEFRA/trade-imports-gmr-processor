using System.Linq.Expressions;
using Defra.TradeImportsGmrFinder.Domain.Events;
using GmrProcessor.Config;
using GmrProcessor.Data;
using GmrProcessor.Data.Common;
using GmrProcessor.Data.ImportGmrMatching;
using GmrProcessor.Processors.ImportGmrMatching;
using GmrProcessor.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using TestFixtures;

namespace GmrProcessor.Tests.Processors.ImportGmrMatching;

public class ImportMatchedGmrsProcessorTests
{
    private readonly Mock<IMongoContext> _mockMongoContext = new();
    private readonly Mock<IMatchReferenceRepository> _mockMatchReferenceRepository = new();
    private readonly Mock<IMongoCollectionSet<MatchedImportNotification>> _mockMatchedImportNotificationsCollection =
        new();
    private readonly Mock<ITradeImportsServiceBus> _mockServiceBusSenderService = new();
    private readonly Mock<ILogger<ImportMatchedGmrsProcessor>> _logger = new();
    private readonly ImportMatchedGmrsProcessor _processor;

    public ImportMatchedGmrsProcessorTests()
    {
        _mockMongoContext
            .Setup(x => x.MatchedImportNotifications)
            .Returns(_mockMatchedImportNotificationsCollection.Object);
        _processor = new ImportMatchedGmrsProcessor(
            _mockMongoContext.Object,
            _mockMatchReferenceRepository.Object,
            _mockServiceBusSenderService.Object,
            Options.Create(
                new TradeImportsServiceBusOptions
                {
                    Eta = new ServiceBusQueue { ConnectionString = "", QueueName = "EtaQueueName" },
                    ImportMatchResult = new ServiceBusQueue
                    {
                        ConnectionString = "",
                        QueueName = "ImportMatchResultQueueName",
                    },
                }
            ),
            _logger.Object
        );
    }

    [Fact]
    public async Task Process_WhenUnmatchedImportsExist_CreatesMatchedImportNotifications()
    {
        var mrn = CustomsDeclarationFixtures.GenerateMrn();
        var matchedGmr = new MatchedGmr { Mrn = mrn, Gmr = GmrFixtures.GmrFixture().Create() };

        var importRef1 = ImportPreNotificationFixtures.GenerateRandomReference();
        var importRef2 = ImportPreNotificationFixtures.GenerateRandomReference();
        var transitId = ImportPreNotificationFixtures.GenerateRandomReference();

        _mockMatchReferenceRepository
            .Setup(x => x.GetChedsByMrn(mrn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { importRef1, importRef2, transitId });

        // Mock that no items are already matched
        _mockMatchedImportNotificationsCollection
            .Setup(x =>
                x.FindMany<MatchedImportNotification>(
                    It.IsAny<Expression<Func<MatchedImportNotification, bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);

        List<WriteModel<MatchedImportNotification>>? capturedOps = null;
        _mockMatchedImportNotificationsCollection
            .Setup(x =>
                x.BulkWrite(It.IsAny<List<WriteModel<MatchedImportNotification>>>(), It.IsAny<CancellationToken>())
            )
            .Callback<List<WriteModel<MatchedImportNotification>>, CancellationToken>((ops, _) => capturedOps = ops);

        var result = await _processor.Process(matchedGmr, CancellationToken.None);

        result.Should().Be(ImportMatchedGmrsProcessorResult.UpdatedIpaffs);

        Assert.NotNull(capturedOps);
        Assert.Equal(3, capturedOps.Count);

        foreach (var op in capturedOps)
        {
            var replaceOp = Assert.IsType<ReplaceOneModel<MatchedImportNotification>>(op);
            Assert.True(replaceOp.IsUpsert);

            // Access the replacement document
            var notification = replaceOp.Replacement;
            Assert.Equal(mrn, notification.Mrn);
            Assert.NotNull(notification.CreatedDateTime);
            Assert.Contains(notification.Id, new[] { importRef1, importRef2, transitId });
        }

        _mockServiceBusSenderService.Verify(
            s =>
                s.SendMessagesAsync(
                    It.Is<IEnumerable<ImportMatchMessage>>(m => m.Count() == 3),
                    "ImportMatchResultQueueName",
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Process_WhenNoRelatedImports_LogsAndSkips()
    {
        var mrn = CustomsDeclarationFixtures.GenerateMrn();
        var matchedGmr = new MatchedGmr { Mrn = mrn, Gmr = GmrFixtures.GmrFixture().Create() };

        _mockMatchReferenceRepository
            .Setup(x => x.GetChedsByMrn(mrn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());
        _mockMatchedImportNotificationsCollection
            .Setup(x =>
                x.FindMany<MatchedImportNotification>(
                    It.IsAny<Expression<Func<MatchedImportNotification, bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);

        var result = await _processor.Process(matchedGmr, CancellationToken.None);
        result.Should().Be(ImportMatchedGmrsProcessorResult.NoRelatedImportsFound);

        _mockServiceBusSenderService.Verify(
            s =>
                s.SendMessagesAsync(
                    It.IsAny<IEnumerable<ImportMatchMessage>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        _mockMatchedImportNotificationsCollection.Verify(
            x => x.BulkWrite(It.IsAny<List<WriteModel<MatchedImportNotification>>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        _logger.Verify(
            l =>
                l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>(
                        (state, _) =>
                            state.ToString()
                            == $"ImportGmrMatching: Skipping {matchedGmr.Mrn} because no related imports have been found"
                    ),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Process_WhenPreviouslyMatchedGmrExist_NoNewMatchesAreCreated()
    {
        var mrn = CustomsDeclarationFixtures.GenerateMrn();
        var matchedGmr = new MatchedGmr { Mrn = mrn, Gmr = GmrFixtures.GmrFixture().Create() };

        var transitId = ImportPreNotificationFixtures.GenerateRandomReference();

        _mockMatchReferenceRepository
            .Setup(x => x.GetChedsByMrn(mrn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { transitId });

        // Mock that items are already matched
        var previousMatch = new MatchedImportNotification { Id = transitId, Mrn = mrn };
        _mockMatchedImportNotificationsCollection
            .Setup(x =>
                x.FindMany<MatchedImportNotification>(
                    It.IsAny<Expression<Func<MatchedImportNotification, bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([previousMatch]);

        var result = await _processor.Process(matchedGmr, CancellationToken.None);
        result.Should().Be(ImportMatchedGmrsProcessorResult.NoUpdatesFound);

        _mockMatchedImportNotificationsCollection.Verify(
            x => x.BulkWrite(It.IsAny<List<WriteModel<MatchedImportNotification>>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Process_WhenPreviouslyMatchedGmrExist_LogsNoUpdatesMessage()
    {
        var mrn = CustomsDeclarationFixtures.GenerateMrn();
        var matchedGmr = new MatchedGmr { Mrn = mrn, Gmr = GmrFixtures.GmrFixture().Create() };

        var transitId = ImportPreNotificationFixtures.GenerateRandomReference();

        _mockMatchReferenceRepository
            .Setup(x => x.GetChedsByMrn(mrn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { transitId });

        _mockMatchedImportNotificationsCollection
            .Setup(x =>
                x.FindMany<MatchedImportNotification>(
                    It.IsAny<Expression<Func<MatchedImportNotification, bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([new MatchedImportNotification { Id = transitId, Mrn = mrn }]);

        var result = await _processor.Process(matchedGmr, CancellationToken.None);
        result.Should().Be(ImportMatchedGmrsProcessorResult.NoUpdatesFound);

        _logger.Verify(
            l =>
                l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>(
                        (state, _) =>
                            state.ToString()
                            == $"ImportGmrMatching: Received matched GMR {matchedGmr.Gmr.GmrId}, but no updates to send"
                    ),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Process_WhenUnmatchedImportsExist_LogsUpdatingIpaffsMessage()
    {
        var mrn = CustomsDeclarationFixtures.GenerateMrn();
        var matchedGmr = new MatchedGmr { Mrn = mrn, Gmr = GmrFixtures.GmrFixture().Create() };

        var importRef = ImportPreNotificationFixtures.GenerateRandomReference();
        var transitId = ImportPreNotificationFixtures.GenerateRandomReference();

        _mockMatchReferenceRepository
            .Setup(x => x.GetChedsByMrn(mrn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { importRef, transitId });

        _mockMatchedImportNotificationsCollection
            .Setup(x =>
                x.FindMany<MatchedImportNotification>(
                    It.IsAny<Expression<Func<MatchedImportNotification, bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);

        var result = await _processor.Process(matchedGmr, CancellationToken.None);
        result.Should().Be(ImportMatchedGmrsProcessorResult.UpdatedIpaffs);

        _logger.Verify(
            l =>
                l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>(
                        (state, _) =>
                            state.ToString()
                            == $"ImportGmrMatching: Received matched GMR {matchedGmr.Gmr.GmrId}, updating Ipaffs with Mrns: {importRef},{transitId}"
                    ),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }
}
