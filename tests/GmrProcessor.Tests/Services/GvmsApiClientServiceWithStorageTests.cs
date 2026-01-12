using System.Text.Json;
using GmrProcessor.Data;
using GmrProcessor.Services;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;
using TestFixtures;

namespace GmrProcessor.Tests.Services;

public class GvmsApiClientServiceWithStorageTests
{
    private readonly Mock<IGvmsApiClientService> _gvmsApiClient = new();
    private readonly Mock<IMongoContext> _mongoContext = new();
    private readonly Mock<IMongoCollectionSet<MessageAudit>> _messageAudits = new();
    private readonly Mock<ILogger<GvmsApiClientServiceWithStorage>> _logger = new();
    private readonly GvmsApiClientServiceWithStorage _service;

    public GvmsApiClientServiceWithStorageTests()
    {
        _mongoContext.Setup(m => m.MessageAudits).Returns(_messageAudits.Object);
        _service = new GvmsApiClientServiceWithStorage(_gvmsApiClient.Object, _mongoContext.Object, _logger.Object);
    }

    [Fact]
    public async Task PlaceOrReleaseHold_PlacesHoldFirst()
    {
        var gmrId = GmrFixtures.GenerateGmrId();
        const bool holdStatus = true;

        _gvmsApiClient
            .Setup(s => s.PlaceOrReleaseHold(gmrId, holdStatus, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _messageAudits
            .Setup(m => m.BulkWrite(It.IsAny<List<WriteModel<MessageAudit>>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.PlaceOrReleaseHold(gmrId, holdStatus, CancellationToken.None);

        _gvmsApiClient.Verify(s => s.PlaceOrReleaseHold(gmrId, holdStatus, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task PlaceOrReleaseHold_StoresRequestInMongoDB()
    {
        var gmrId = GmrFixtures.GenerateGmrId();
        const bool holdStatus = true;

        _gvmsApiClient
            .Setup(s => s.PlaceOrReleaseHold(gmrId, holdStatus, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        List<WriteModel<MessageAudit>>? capturedModels = null;

        _messageAudits
            .Setup(m => m.BulkWrite(It.IsAny<List<WriteModel<MessageAudit>>>(), It.IsAny<CancellationToken>()))
            .Callback(
                (List<WriteModel<MessageAudit>> models, CancellationToken _) =>
                {
                    capturedModels = models;
                }
            )
            .Returns(Task.CompletedTask);

        await _service.PlaceOrReleaseHold(gmrId, holdStatus, CancellationToken.None);

        Assert.NotNull(capturedModels);
        Assert.Single(capturedModels);

        _messageAudits.Verify(
            m => m.BulkWrite(It.IsAny<List<WriteModel<MessageAudit>>>(), CancellationToken.None),
            Times.Once
        );
    }

    [Fact]
    public async Task PlaceOrReleaseHold_SerializesMessageCorrectly()
    {
        var gmrId = GmrFixtures.GenerateGmrId();
        const bool holdStatus = true;

        _gvmsApiClient
            .Setup(s => s.PlaceOrReleaseHold(gmrId, holdStatus, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        InsertOneModel<MessageAudit>? capturedModel = null;

        _messageAudits
            .Setup(m => m.BulkWrite(It.IsAny<List<WriteModel<MessageAudit>>>(), It.IsAny<CancellationToken>()))
            .Callback(
                (List<WriteModel<MessageAudit>> models, CancellationToken _) =>
                {
                    capturedModel = models[0] as InsertOneModel<MessageAudit>;
                }
            )
            .Returns(Task.CompletedTask);

        await _service.PlaceOrReleaseHold(gmrId, holdStatus, CancellationToken.None);

        Assert.NotNull(capturedModel);
        var messageAudit = capturedModel.Document;
        Assert.NotNull(messageAudit);

        var deserializedBody = JsonSerializer.Deserialize<JsonElement>(messageAudit.MessageBody);
        Assert.Equal(gmrId, deserializedBody.GetProperty("gmrId").GetString());
        Assert.Equal(holdStatus, deserializedBody.GetProperty("holdStatus").GetBoolean());
    }

    [Fact]
    public async Task PlaceOrReleaseHold_DoesNotThrowOnStorageFailure()
    {
        var gmrId = GmrFixtures.GenerateGmrId();
        const bool holdStatus = true;

        _gvmsApiClient
            .Setup(s => s.PlaceOrReleaseHold(gmrId, holdStatus, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _messageAudits
            .Setup(m => m.BulkWrite(It.IsAny<List<WriteModel<MessageAudit>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("MongoDB connection failed"));

        await _service.PlaceOrReleaseHold(gmrId, holdStatus, CancellationToken.None);

        _gvmsApiClient.Verify(s => s.PlaceOrReleaseHold(gmrId, holdStatus, CancellationToken.None), Times.Once);
    }
}
