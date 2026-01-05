using System.Linq.Expressions;
using Defra.TradeImportsDataApi.Api.Client;
using Defra.TradeImportsGmrFinder.Domain.Events;
using GmrProcessor.Config;
using GmrProcessor.Data;
using GmrProcessor.Processors.ImportGmrMatching;
using GmrProcessor.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Moq;
using TestFixtures;

namespace GmrProcessor.Tests.Processors.ImportGmrMatching;

public class ImportMatchedGmrsProcessorTests
{
    private readonly Mock<ITradeImportsDataApiClient> _mockApiClient = new();
    private readonly Mock<IMongoContext> _mockMongoContext = new();
    private readonly Mock<IMongoCollectionSet<MatchedImportNotification>> _mockMatchedImportNotificationsCollection =
        new();
    private readonly Mock<IMongoCollectionSet<ImportTransit>> _mockImportTransitsCollection = new();
    private readonly Mock<ITradeImportsServiceBus> _mockServiceBusSenderService = new();
    private readonly Mock<ILogger<ImportMatchedGmrsProcessor>> _logger = new();
    private readonly ImportMatchedGmrsProcessor _processor;

    public ImportMatchedGmrsProcessorTests()
    {
        _mockMongoContext
            .Setup(x => x.MatchedImportNotifications)
            .Returns(_mockMatchedImportNotificationsCollection.Object);
        _mockMongoContext.Setup(x => x.ImportTransits).Returns(_mockImportTransitsCollection.Object);
        _processor = new ImportMatchedGmrsProcessor(
            _mockApiClient.Object,
            _mockMongoContext.Object,
            _mockServiceBusSenderService.Object,
            Options.Create(
                new TradeImportsServiceBusOptions
                {
                    ConnectionString = "",
                    EtaQueueName = "EtaQueueName",
                    ImportMatchResultQueueName = "ImportMatchResultQueueName",
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

        // Mock API response with import pre-notifications
        var apiResponse = new ImportPreNotificationsResponse([
            ImportPreNotificationFixtures
                .ImportPreNotificationResponseFixture(
                    ImportPreNotificationFixtures.ImportPreNotificationFixture(importRef1).Create()
                )
                .Create(),
            ImportPreNotificationFixtures
                .ImportPreNotificationResponseFixture(
                    ImportPreNotificationFixtures.ImportPreNotificationFixture(importRef2).Create()
                )
                .Create(),
        ]);

        _mockApiClient
            .Setup(x => x.GetImportPreNotificationsByMrn(mrn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        // Mock transit data
        var transits = new List<ImportTransit>
        {
            new()
            {
                Id = transitId,
                Mrn = mrn,
                TransitOverrideRequired = false,
            },
        };

        _mockImportTransitsCollection
            .Setup(x =>
                x.FindMany<ImportTransit>(
                    It.IsAny<Expression<Func<ImportTransit, bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(transits);

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

        await _processor.Process(matchedGmr, CancellationToken.None);

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

        _mockApiClient
            .Setup(x => x.GetImportPreNotificationsByMrn(mrn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportPreNotificationsResponse([]));
        _mockImportTransitsCollection
            .Setup(x =>
                x.FindMany<ImportTransit>(
                    It.IsAny<Expression<Func<ImportTransit, bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);
        _mockMatchedImportNotificationsCollection
            .Setup(x =>
                x.FindMany<MatchedImportNotification>(
                    It.IsAny<Expression<Func<MatchedImportNotification, bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);

        await _processor.Process(matchedGmr, CancellationToken.None);

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
                            state.ToString() == $"Skipping {matchedGmr.Mrn} because no related imports have been found"
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

        // Mock API response with import pre-notifications
        _mockApiClient
            .Setup(x => x.GetImportPreNotificationsByMrn(mrn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportPreNotificationsResponse([]));

        // Mock transit data
        var transit = new ImportTransit
        {
            Id = transitId,
            Mrn = mrn,
            TransitOverrideRequired = false,
        };
        _mockImportTransitsCollection
            .Setup(x =>
                x.FindMany<ImportTransit>(
                    It.IsAny<Expression<Func<ImportTransit, bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([transit]);

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

        await _processor.Process(matchedGmr, CancellationToken.None);

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

        _mockApiClient
            .Setup(x => x.GetImportPreNotificationsByMrn(mrn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportPreNotificationsResponse([]));

        var transitId = ImportPreNotificationFixtures.GenerateRandomReference();
        _mockImportTransitsCollection
            .Setup(x =>
                x.FindMany<ImportTransit>(
                    It.IsAny<Expression<Func<ImportTransit, bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([
                new ImportTransit
                {
                    Id = transitId,
                    Mrn = mrn,
                    TransitOverrideRequired = false,
                },
            ]);

        _mockMatchedImportNotificationsCollection
            .Setup(x =>
                x.FindMany<MatchedImportNotification>(
                    It.IsAny<Expression<Func<MatchedImportNotification, bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([new MatchedImportNotification { Id = transitId, Mrn = mrn }]);

        await _processor.Process(matchedGmr, CancellationToken.None);

        _logger.Verify(
            l =>
                l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>(
                        (state, _) =>
                            state.ToString() == $"Received matched GMR {matchedGmr.Gmr.GmrId}, but no updates to send"
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

        _mockApiClient
            .Setup(x => x.GetImportPreNotificationsByMrn(mrn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ImportPreNotificationsResponse([
                    ImportPreNotificationFixtures
                        .ImportPreNotificationResponseFixture(
                            ImportPreNotificationFixtures.ImportPreNotificationFixture(importRef).Create()
                        )
                        .Create(),
                ])
            );

        _mockImportTransitsCollection
            .Setup(x =>
                x.FindMany<ImportTransit>(
                    It.IsAny<Expression<Func<ImportTransit, bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([
                new ImportTransit
                {
                    Id = transitId,
                    Mrn = mrn,
                    TransitOverrideRequired = false,
                },
            ]);

        _mockMatchedImportNotificationsCollection
            .Setup(x =>
                x.FindMany<MatchedImportNotification>(
                    It.IsAny<Expression<Func<MatchedImportNotification, bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);

        await _processor.Process(matchedGmr, CancellationToken.None);

        _logger.Verify(
            l =>
                l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>(
                        (state, _) =>
                            state.ToString()
                            == $"Received matched GMR {matchedGmr.Gmr.GmrId}, updating Ipaffs with Mrns: {importRef},{transitId}"
                    ),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }
}
