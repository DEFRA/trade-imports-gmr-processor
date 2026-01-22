using System.Linq.Expressions;
using System.Text.Json;
using Defra.TradeImportsGmrFinder.GvmsClient.Client;
using GmrProcessor.Config;
using GmrProcessor.Data;
using GmrProcessor.Data.Auditing;
using GmrProcessor.Data.Common;
using GmrProcessor.Data.Gto;
using GmrProcessor.Metrics;
using GmrProcessor.Processors.Gto;
using GmrProcessor.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Moq;
using TestFixtures;

namespace GmrProcessor.Tests.Services;

public class GvmsHoldServiceTests
{
    private readonly Mock<ILogger<GvmsHoldService>> _logger = new();
    private readonly Mock<IGtoMatchedGmrCollection> _matchedGmrRepository = new();
    private readonly Mock<IGtoImportTransitCollection> _importTransitRepository = new();
    private readonly Mock<IGtoGmrCollection> _gtoGmrCollection = new();
    private readonly Mock<IGvmsApiClient> _gvmsApiClient = new();
    private readonly Mock<IMongoContext> _mongoContext = new();
    private readonly Mock<IMongoCollectionSet<MessageAudit>> _messageAudits = new();
    private readonly Mock<IOptions<FeatureOptions>> _featureOptions = new();
    private readonly Mock<IGvmsApiMetrics> _metrics = new();

    public GvmsHoldServiceTests()
    {
        _mongoContext.Setup(m => m.GtoGmr).Returns(_gtoGmrCollection.Object);
        _mongoContext.Setup(m => m.MessageAudits).Returns(_messageAudits.Object);
        _featureOptions
            .Setup(f => f.Value)
            .Returns(new FeatureOptions { EnableStoreOutboundMessages = false, EnableGvmsApiClientHold = true });
        _metrics
            .Setup(m => m.RecordRequest(It.IsAny<string>(), It.IsAny<Task>()))
            .Returns<string, Task>((_, task) => task);
    }

    private GvmsHoldService CreateService()
    {
        return new GvmsHoldService(
            _logger.Object,
            _featureOptions.Object,
            _mongoContext.Object,
            _gvmsApiClient.Object,
            _matchedGmrRepository.Object,
            _importTransitRepository.Object,
            _gtoGmrCollection.Object,
            _metrics.Object
        );
    }

    [Fact]
    public async Task PlaceOrReleaseHold_WhenGmrNotFound_ReturnsNull()
    {
        var gmrId = GmrFixtures.GenerateGmrId();

        _gtoGmrCollection
            .Setup(c => c.FindOne(It.IsAny<Expression<Func<GtoGmr, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GtoGmr?)null);

        var service = CreateService();
        var result = await service.PlaceOrReleaseHold(gmrId, CancellationToken.None);

        result.Should().Be(GvmsHoldResult.NoHoldChange);
        _logger.Verify(
            l =>
                l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>(
                        (state, _) =>
                            state
                                .ToString()!
                                .Contains($"Tried to place or release a hold on {gmrId} which was not found")
                    ),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task PlaceOrReleaseHold_WhenHoldStatusAlreadyMatches_AndHoldIsTrue_ReturnsNull()
    {
        var gmrId = GmrFixtures.GenerateGmrId();
        var mrns = new List<string> { "mrn1" };
        var gmr = GmrFixtures.GmrFixture().With(g => g.GmrId, gmrId).Create();
        var gtoGmr = new GtoGmr
        {
            Id = gmrId,
            Gmr = gmr,
            HoldStatus = true,
            UpdatedDateTime = DateTime.UtcNow,
        };
        var importTransit = new ImportTransit
        {
            Id = "id1",
            Mrn = "mrn1",
            TransitOverrideRequired = true,
        };

        _gtoGmrCollection
            .Setup(c => c.FindOne(It.IsAny<Expression<Func<GtoGmr, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gtoGmr);
        _matchedGmrRepository.Setup(r => r.GetRelatedMrns(gmrId, It.IsAny<CancellationToken>())).ReturnsAsync(mrns);
        _importTransitRepository
            .Setup(r => r.GetByMrns(mrns, It.IsAny<CancellationToken>()))
            .ReturnsAsync([importTransit]);

        var service = CreateService();
        var result = await service.PlaceOrReleaseHold(gmrId, CancellationToken.None);

        result.Should().Be(GvmsHoldResult.NoHoldChange);
        _logger.Verify(
            l =>
                l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>(
                        (state, _) => state.ToString()!.Contains($"Matched GMR {gmrId} is already on hold")
                    ),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
        _gvmsApiClient.Verify(
            c => c.HoldGmr(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task PlaceOrReleaseHold_WhenHoldStatusAlreadyMatches_AndHoldIsFalse_ReturnsNull()
    {
        var gmrId = GmrFixtures.GenerateGmrId();
        var mrns = new List<string> { "mrn1" };
        var gmr = GmrFixtures.GmrFixture().With(g => g.GmrId, gmrId).Create();
        var gtoGmr = new GtoGmr
        {
            Id = gmrId,
            Gmr = gmr,
            HoldStatus = false,
            UpdatedDateTime = DateTime.UtcNow,
        };
        var importTransit = new ImportTransit
        {
            Id = "id1",
            Mrn = "mrn1",
            TransitOverrideRequired = false,
        };

        _gtoGmrCollection
            .Setup(c => c.FindOne(It.IsAny<Expression<Func<GtoGmr, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gtoGmr);
        _matchedGmrRepository.Setup(r => r.GetRelatedMrns(gmrId, It.IsAny<CancellationToken>())).ReturnsAsync(mrns);
        _importTransitRepository
            .Setup(r => r.GetByMrns(mrns, It.IsAny<CancellationToken>()))
            .ReturnsAsync([importTransit]);

        var service = CreateService();
        var result = await service.PlaceOrReleaseHold(gmrId, CancellationToken.None);

        result.Should().Be(GvmsHoldResult.NoHoldChange);
        _logger.Verify(
            l =>
                l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>(
                        (state, _) => state.ToString()!.Contains($"Matched GMR {gmrId} is already released")
                    ),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
        _gvmsApiClient.Verify(
            c => c.HoldGmr(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task PlaceOrReleaseHold_WhenImportTransitsRequireOverride_PlacesHold()
    {
        var gmrId = GmrFixtures.GenerateGmrId();
        var mrns = new List<string> { "mrn1", "mrn2" };
        var gmr = GmrFixtures.GmrFixture().With(g => g.GmrId, gmrId).Create();
        var gtoGmr = new GtoGmr
        {
            Id = gmrId,
            Gmr = gmr,
            HoldStatus = false,
            UpdatedDateTime = DateTime.UtcNow,
        };
        var importTransitOne = new ImportTransit
        {
            Id = "id1",
            Mrn = "mrn1",
            TransitOverrideRequired = true,
        };
        var importTransitTwo = new ImportTransit
        {
            Id = "id2",
            Mrn = "mrn2",
            TransitOverrideRequired = false,
        };

        _gtoGmrCollection
            .Setup(c => c.FindOne(It.IsAny<Expression<Func<GtoGmr, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gtoGmr);
        _matchedGmrRepository.Setup(r => r.GetRelatedMrns(gmrId, It.IsAny<CancellationToken>())).ReturnsAsync(mrns);
        _importTransitRepository
            .Setup(r => r.GetByMrns(mrns, It.IsAny<CancellationToken>()))
            .ReturnsAsync([importTransitOne, importTransitTwo]);
        _gvmsApiClient.Setup(c => c.HoldGmr(gmrId, true, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _gtoGmrCollection
            .Setup(c => c.UpdateHoldStatus(gmrId, true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var result = await service.PlaceOrReleaseHold(gmrId, CancellationToken.None);

        result.Should().Be(GvmsHoldResult.HoldPlaced);
        _gvmsApiClient.Verify(c => c.HoldGmr(gmrId, true, CancellationToken.None), Times.Once);
        _gtoGmrCollection.Verify(c => c.UpdateHoldStatus(gmrId, true, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task PlaceOrReleaseHold_WhenNoImportTransitsRequireOverride_ReleasesHold()
    {
        var gmrId = GmrFixtures.GenerateGmrId();
        var mrns = new List<string> { "mrn1", "mrn2" };
        var gmr = GmrFixtures.GmrFixture().With(g => g.GmrId, gmrId).Create();
        var gtoGmr = new GtoGmr
        {
            Id = gmrId,
            Gmr = gmr,
            HoldStatus = true,
            UpdatedDateTime = DateTime.UtcNow,
        };
        var importTransitOne = new ImportTransit
        {
            Id = "id1",
            Mrn = "mrn1",
            TransitOverrideRequired = false,
        };
        var importTransitTwo = new ImportTransit
        {
            Id = "id2",
            Mrn = "mrn2",
            TransitOverrideRequired = false,
        };

        _gtoGmrCollection
            .Setup(c => c.FindOne(It.IsAny<Expression<Func<GtoGmr, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gtoGmr);
        _matchedGmrRepository.Setup(r => r.GetRelatedMrns(gmrId, It.IsAny<CancellationToken>())).ReturnsAsync(mrns);
        _importTransitRepository
            .Setup(r => r.GetByMrns(mrns, It.IsAny<CancellationToken>()))
            .ReturnsAsync([importTransitOne, importTransitTwo]);
        _gvmsApiClient.Setup(c => c.HoldGmr(gmrId, false, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _gtoGmrCollection
            .Setup(c => c.UpdateHoldStatus(gmrId, false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var result = await service.PlaceOrReleaseHold(gmrId, CancellationToken.None);

        result.Should().Be(GvmsHoldResult.HoldReleased);
        _gvmsApiClient.Verify(c => c.HoldGmr(gmrId, false, CancellationToken.None), Times.Once);
        _gtoGmrCollection.Verify(c => c.UpdateHoldStatus(gmrId, false, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task PlaceOrReleaseHold_WhenNoImportTransitsFound_TakesNoAction()
    {
        var gmrId = GmrFixtures.GenerateGmrId();
        var mrns = new List<string> { "mrn1", "mrn2" };
        var gmr = GmrFixtures.GmrFixture().With(g => g.GmrId, gmrId).Create();
        var gtoGmr = new GtoGmr
        {
            Id = gmrId,
            Gmr = gmr,
            HoldStatus = true,
            UpdatedDateTime = DateTime.UtcNow,
        };

        _gtoGmrCollection
            .Setup(c => c.FindOne(It.IsAny<Expression<Func<GtoGmr, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gtoGmr);
        _matchedGmrRepository.Setup(r => r.GetRelatedMrns(gmrId, It.IsAny<CancellationToken>())).ReturnsAsync(mrns);
        _importTransitRepository.Setup(r => r.GetByMrns(mrns, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _gvmsApiClient.Setup(c => c.HoldGmr(gmrId, false, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _gtoGmrCollection
            .Setup(c => c.UpdateHoldStatus(gmrId, false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var result = await service.PlaceOrReleaseHold(gmrId, CancellationToken.None);

        result.Should().Be(GvmsHoldResult.NoHoldChange);
        _gvmsApiClient.Verify(c => c.HoldGmr(gmrId, It.IsAny<bool>(), CancellationToken.None), Times.Never);
        _gtoGmrCollection.Verify(c => c.UpdateHoldStatus(gmrId, It.IsAny<bool>(), CancellationToken.None), Times.Never);
    }

    [Fact]
    public async Task PlaceOrReleaseHold_CallsGvmsApiClient_AndUpdatesDatabase()
    {
        var gmrId = GmrFixtures.GenerateGmrId();
        var mrns = new List<string> { "mrn1" };
        var gmr = GmrFixtures.GmrFixture().With(g => g.GmrId, gmrId).Create();
        var gtoGmr = new GtoGmr
        {
            Id = gmrId,
            Gmr = gmr,
            HoldStatus = false,
            UpdatedDateTime = DateTime.UtcNow,
        };
        var importTransit = new ImportTransit
        {
            Id = "id1",
            Mrn = "mrn1",
            TransitOverrideRequired = true,
        };

        _gtoGmrCollection
            .Setup(c => c.FindOne(It.IsAny<Expression<Func<GtoGmr, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gtoGmr);
        _matchedGmrRepository.Setup(r => r.GetRelatedMrns(gmrId, It.IsAny<CancellationToken>())).ReturnsAsync(mrns);
        _importTransitRepository
            .Setup(r => r.GetByMrns(mrns, It.IsAny<CancellationToken>()))
            .ReturnsAsync([importTransit]);
        _gvmsApiClient.Setup(c => c.HoldGmr(gmrId, true, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _gtoGmrCollection
            .Setup(c => c.UpdateHoldStatus(gmrId, true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await service.PlaceOrReleaseHold(gmrId, CancellationToken.None);

        _gvmsApiClient.Verify(c => c.HoldGmr(gmrId, true, CancellationToken.None), Times.Once);
        _gtoGmrCollection.Verify(c => c.UpdateHoldStatus(gmrId, true, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task PlaceOrReleaseHold_LogsAppropriateMessages()
    {
        var gmrId = GmrFixtures.GenerateGmrId();
        var mrns = new List<string> { "mrn1" };
        var gmr = GmrFixtures.GmrFixture().With(g => g.GmrId, gmrId).Create();
        var gtoGmr = new GtoGmr
        {
            Id = gmrId,
            Gmr = gmr,
            HoldStatus = false,
            UpdatedDateTime = DateTime.UtcNow,
        };
        var importTransit = new ImportTransit
        {
            Id = "id1",
            Mrn = "mrn1",
            TransitOverrideRequired = true,
        };

        _gtoGmrCollection
            .Setup(c => c.FindOne(It.IsAny<Expression<Func<GtoGmr, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gtoGmr);
        _matchedGmrRepository.Setup(r => r.GetRelatedMrns(gmrId, It.IsAny<CancellationToken>())).ReturnsAsync(mrns);
        _importTransitRepository
            .Setup(r => r.GetByMrns(mrns, It.IsAny<CancellationToken>()))
            .ReturnsAsync([importTransit]);
        _gvmsApiClient.Setup(c => c.HoldGmr(gmrId, true, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _gtoGmrCollection
            .Setup(c => c.UpdateHoldStatus(gmrId, true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await service.PlaceOrReleaseHold(gmrId, CancellationToken.None);

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

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task PlaceOrReleaseHold_StoresMessageAudit_DependantOnFeatureFlag(bool featureEnabled, int callCount)
    {
        var gmrId = GmrFixtures.GenerateGmrId();
        var mrns = new List<string> { "mrn1" };
        var gmr = GmrFixtures.GmrFixture().With(g => g.GmrId, gmrId).Create();
        var gtoGmr = new GtoGmr
        {
            Id = gmrId,
            Gmr = gmr,
            HoldStatus = false,
            UpdatedDateTime = DateTime.UtcNow,
        };
        var importTransit = new ImportTransit
        {
            Id = "id1",
            Mrn = "mrn1",
            TransitOverrideRequired = true,
        };

        _featureOptions
            .Setup(f => f.Value)
            .Returns(
                new FeatureOptions { EnableStoreOutboundMessages = featureEnabled, EnableGvmsApiClientHold = true }
            );
        _gtoGmrCollection
            .Setup(c => c.FindOne(It.IsAny<Expression<Func<GtoGmr, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gtoGmr);
        _matchedGmrRepository.Setup(r => r.GetRelatedMrns(gmrId, It.IsAny<CancellationToken>())).ReturnsAsync(mrns);
        _importTransitRepository
            .Setup(r => r.GetByMrns(mrns, It.IsAny<CancellationToken>()))
            .ReturnsAsync([importTransit]);
        _gvmsApiClient.Setup(c => c.HoldGmr(gmrId, true, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _gtoGmrCollection
            .Setup(c => c.UpdateHoldStatus(gmrId, true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _messageAudits
            .Setup(m => m.BulkWrite(It.IsAny<List<WriteModel<MessageAudit>>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await service.PlaceOrReleaseHold(gmrId, CancellationToken.None);

        _messageAudits.Verify(
            m => m.BulkWrite(It.IsAny<List<WriteModel<MessageAudit>>>(), CancellationToken.None),
            Times.Exactly(callCount)
        );
    }

    [Fact]
    public async Task PlaceOrReleaseHold_SerializesMessageBodyCorrectly()
    {
        var gmrId = GmrFixtures.GenerateGmrId();
        var mrns = new List<string> { "mrn1" };
        var gmr = GmrFixtures.GmrFixture().With(g => g.GmrId, gmrId).Create();
        var gtoGmr = new GtoGmr
        {
            Id = gmrId,
            Gmr = gmr,
            HoldStatus = false,
            UpdatedDateTime = DateTime.UtcNow,
        };
        var importTransit = new ImportTransit
        {
            Id = "id1",
            Mrn = "mrn1",
            TransitOverrideRequired = true,
        };
        InsertOneModel<MessageAudit>? capturedModel = null;

        _featureOptions
            .Setup(f => f.Value)
            .Returns(new FeatureOptions { EnableStoreOutboundMessages = true, EnableGvmsApiClientHold = true });
        _gtoGmrCollection
            .Setup(c => c.FindOne(It.IsAny<Expression<Func<GtoGmr, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gtoGmr);
        _matchedGmrRepository.Setup(r => r.GetRelatedMrns(gmrId, It.IsAny<CancellationToken>())).ReturnsAsync(mrns);
        _importTransitRepository
            .Setup(r => r.GetByMrns(mrns, It.IsAny<CancellationToken>()))
            .ReturnsAsync([importTransit]);
        _gvmsApiClient.Setup(c => c.HoldGmr(gmrId, true, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _gtoGmrCollection
            .Setup(c => c.UpdateHoldStatus(gmrId, true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _messageAudits
            .Setup(m => m.BulkWrite(It.IsAny<List<WriteModel<MessageAudit>>>(), It.IsAny<CancellationToken>()))
            .Callback(
                (List<WriteModel<MessageAudit>> models, CancellationToken _) =>
                {
                    capturedModel = models[0] as InsertOneModel<MessageAudit>;
                }
            )
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await service.PlaceOrReleaseHold(gmrId, CancellationToken.None);

        Assert.NotNull(capturedModel);
        var messageAudit = capturedModel.Document;
        Assert.NotNull(messageAudit);

        messageAudit.Direction.Should().Be(MessageDirection.Outbound);
        messageAudit.IntegrationType.Should().Be(IntegrationType.GvmsApi);
        messageAudit.Target.Should().Be("GVMS Hold API");
        messageAudit.MessageType.Should().Be("GvmsHoldRequest");

        var deserializedBody = JsonSerializer.Deserialize<JsonElement>(messageAudit.MessageBody);
        Assert.Equal(gmrId, deserializedBody.GetProperty("GmrId").GetString());
        Assert.True(deserializedBody.GetProperty("Hold").GetBoolean());

        var mrnsArray = deserializedBody.GetProperty("Mrns");
        Assert.Equal(JsonValueKind.Array, mrnsArray.ValueKind);
        Assert.Single(mrnsArray.EnumerateArray());
        Assert.Equal("mrn1", mrnsArray[0].GetString());
    }

    [Fact]
    public async Task PlaceOrReleaseHold_StoresMessage_EvenWhenApiCallFails()
    {
        var gmrId = GmrFixtures.GenerateGmrId();
        var mrns = new List<string> { "mrn1" };
        var gmr = GmrFixtures.GmrFixture().With(g => g.GmrId, gmrId).Create();
        var gtoGmr = new GtoGmr
        {
            Id = gmrId,
            Gmr = gmr,
            HoldStatus = false,
            UpdatedDateTime = DateTime.UtcNow,
        };
        var importTransit = new ImportTransit
        {
            Id = "id1",
            Mrn = "mrn1",
            TransitOverrideRequired = true,
        };

        _featureOptions
            .Setup(f => f.Value)
            .Returns(new FeatureOptions { EnableStoreOutboundMessages = true, EnableGvmsApiClientHold = true });
        _gtoGmrCollection
            .Setup(c => c.FindOne(It.IsAny<Expression<Func<GtoGmr, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gtoGmr);
        _matchedGmrRepository.Setup(r => r.GetRelatedMrns(gmrId, It.IsAny<CancellationToken>())).ReturnsAsync(mrns);
        _importTransitRepository
            .Setup(r => r.GetByMrns(mrns, It.IsAny<CancellationToken>()))
            .ReturnsAsync([importTransit]);
        _gvmsApiClient
            .Setup(c => c.HoldGmr(gmrId, true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API failure"));
        _gtoGmrCollection
            .Setup(c => c.UpdateHoldStatus(gmrId, true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _messageAudits
            .Setup(m => m.BulkWrite(It.IsAny<List<WriteModel<MessageAudit>>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await service.PlaceOrReleaseHold(gmrId, CancellationToken.None)
        );

        _messageAudits.Verify(
            m => m.BulkWrite(It.IsAny<List<WriteModel<MessageAudit>>>(), CancellationToken.None),
            Times.Once
        );
    }

    [Fact]
    public async Task PlaceOrReleaseHold_DoesNotThrow_WhenStorageFails()
    {
        var gmrId = GmrFixtures.GenerateGmrId();
        var mrns = new List<string> { "mrn1" };
        var gmr = GmrFixtures.GmrFixture().With(g => g.GmrId, gmrId).Create();
        var gtoGmr = new GtoGmr
        {
            Id = gmrId,
            Gmr = gmr,
            HoldStatus = false,
            UpdatedDateTime = DateTime.UtcNow,
        };
        var importTransit = new ImportTransit
        {
            Id = "id1",
            Mrn = "mrn1",
            TransitOverrideRequired = true,
        };

        _featureOptions
            .Setup(f => f.Value)
            .Returns(new FeatureOptions { EnableStoreOutboundMessages = true, EnableGvmsApiClientHold = true });
        _gtoGmrCollection
            .Setup(c => c.FindOne(It.IsAny<Expression<Func<GtoGmr, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gtoGmr);
        _matchedGmrRepository.Setup(r => r.GetRelatedMrns(gmrId, It.IsAny<CancellationToken>())).ReturnsAsync(mrns);
        _importTransitRepository
            .Setup(r => r.GetByMrns(mrns, It.IsAny<CancellationToken>()))
            .ReturnsAsync([importTransit]);
        _gvmsApiClient.Setup(c => c.HoldGmr(gmrId, true, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _gtoGmrCollection
            .Setup(c => c.UpdateHoldStatus(gmrId, true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _messageAudits
            .Setup(m => m.BulkWrite(It.IsAny<List<WriteModel<MessageAudit>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("MongoDB connection failed"));

        var service = CreateService();
        await service.PlaceOrReleaseHold(gmrId, CancellationToken.None);

        _gvmsApiClient.Verify(c => c.HoldGmr(gmrId, true, CancellationToken.None), Times.Once);
        _gtoGmrCollection.Verify(c => c.UpdateHoldStatus(gmrId, true, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task PlaceOrReleaseHold_WhenFeatureFlagDisabled_SkipsApiCallAndReturnsNoHoldChange()
    {
        var gmrId = GmrFixtures.GenerateGmrId();
        var mrns = new List<string> { "mrn1" };
        var gmr = GmrFixtures.GmrFixture().With(g => g.GmrId, gmrId).Create();
        var gtoGmr = new GtoGmr
        {
            Id = gmrId,
            Gmr = gmr,
            HoldStatus = false,
            UpdatedDateTime = DateTime.UtcNow,
        };
        var importTransit = new ImportTransit
        {
            Id = "id1",
            Mrn = "mrn1",
            TransitOverrideRequired = true,
        };

        _featureOptions.Setup(f => f.Value).Returns(new FeatureOptions { EnableGvmsApiClientHold = false });
        _gtoGmrCollection
            .Setup(c => c.FindOne(It.IsAny<Expression<Func<GtoGmr, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gtoGmr);
        _matchedGmrRepository.Setup(r => r.GetRelatedMrns(gmrId, It.IsAny<CancellationToken>())).ReturnsAsync(mrns);
        _importTransitRepository
            .Setup(r => r.GetByMrns(mrns, It.IsAny<CancellationToken>()))
            .ReturnsAsync([importTransit]);

        var service = CreateService();
        var result = await service.PlaceOrReleaseHold(gmrId, CancellationToken.None);

        result.Should().Be(GvmsHoldResult.NoHoldChange);
        _gvmsApiClient.Verify(
            c => c.HoldGmr(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        _gtoGmrCollection.Verify(
            c => c.UpdateHoldStatus(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        _logger.Verify(
            l =>
                l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>(
                        (state, _) => state.ToString() == $"GVMS API client is disabled, skipping API call for {gmrId}"
                    ),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task PlaceOrReleaseHold_When404ResponseAndFlagEnabled_SilentlyIgnoresAndContinues()
    {
        var gmrId = GmrFixtures.GenerateGmrId();
        var mrns = new List<string> { "mrn1" };
        var gmr = GmrFixtures.GmrFixture().With(g => g.GmrId, gmrId).Create();
        var gtoGmr = new GtoGmr
        {
            Id = gmrId,
            Gmr = gmr,
            HoldStatus = false,
            UpdatedDateTime = DateTime.UtcNow,
        };
        var importTransit = new ImportTransit
        {
            Id = "id1",
            Mrn = "mrn1",
            TransitOverrideRequired = true,
        };

        _featureOptions
            .Setup(f => f.Value)
            .Returns(
                new FeatureOptions
                {
                    EnableGvmsApiClientHold = true,
                    EnableGvmsApiClientIgnoreNotFound = true,
                    EnableStoreOutboundMessages = true,
                }
            );
        _gtoGmrCollection
            .Setup(c => c.FindOne(It.IsAny<Expression<Func<GtoGmr, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gtoGmr);
        _matchedGmrRepository.Setup(r => r.GetRelatedMrns(gmrId, It.IsAny<CancellationToken>())).ReturnsAsync(mrns);
        _importTransitRepository
            .Setup(r => r.GetByMrns(mrns, It.IsAny<CancellationToken>()))
            .ReturnsAsync([importTransit]);
        _gvmsApiClient
            .Setup(c => c.HoldGmr(gmrId, true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Not Found", null, System.Net.HttpStatusCode.NotFound));
        _gtoGmrCollection
            .Setup(c => c.UpdateHoldStatus(gmrId, true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _messageAudits
            .Setup(m => m.BulkWrite(It.IsAny<List<WriteModel<MessageAudit>>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var result = await service.PlaceOrReleaseHold(gmrId, CancellationToken.None);

        result.Should().Be(GvmsHoldResult.HoldPlaced);
        _gtoGmrCollection.Verify(c => c.UpdateHoldStatus(gmrId, true, CancellationToken.None), Times.Once);
        _messageAudits.Verify(
            m => m.BulkWrite(It.IsAny<List<WriteModel<MessageAudit>>>(), CancellationToken.None),
            Times.Once
        );
    }

    [Fact]
    public async Task PlaceOrReleaseHold_When404ResponseAndFlagDisabled_ThrowsException()
    {
        var gmrId = GmrFixtures.GenerateGmrId();
        var mrns = new List<string> { "mrn1" };
        var gmr = GmrFixtures.GmrFixture().With(g => g.GmrId, gmrId).Create();
        var gtoGmr = new GtoGmr
        {
            Id = gmrId,
            Gmr = gmr,
            HoldStatus = false,
            UpdatedDateTime = DateTime.UtcNow,
        };
        var importTransit = new ImportTransit
        {
            Id = "id1",
            Mrn = "mrn1",
            TransitOverrideRequired = true,
        };

        _featureOptions
            .Setup(f => f.Value)
            .Returns(
                new FeatureOptions
                {
                    EnableGvmsApiClientHold = true,
                    EnableGvmsApiClientIgnoreNotFound = false,
                    EnableStoreOutboundMessages = true,
                }
            );
        _gtoGmrCollection
            .Setup(c => c.FindOne(It.IsAny<Expression<Func<GtoGmr, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gtoGmr);
        _matchedGmrRepository.Setup(r => r.GetRelatedMrns(gmrId, It.IsAny<CancellationToken>())).ReturnsAsync(mrns);
        _importTransitRepository
            .Setup(r => r.GetByMrns(mrns, It.IsAny<CancellationToken>()))
            .ReturnsAsync([importTransit]);
        _gvmsApiClient
            .Setup(c => c.HoldGmr(gmrId, true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Not Found", null, System.Net.HttpStatusCode.NotFound));
        _messageAudits
            .Setup(m => m.BulkWrite(It.IsAny<List<WriteModel<MessageAudit>>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await service.PlaceOrReleaseHold(gmrId, CancellationToken.None)
        );

        _messageAudits.Verify(
            m => m.BulkWrite(It.IsAny<List<WriteModel<MessageAudit>>>(), CancellationToken.None),
            Times.Once
        );
        _gtoGmrCollection.Verify(
            c => c.UpdateHoldStatus(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
