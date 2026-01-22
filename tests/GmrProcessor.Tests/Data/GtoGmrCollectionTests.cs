using AutoFixture;
using GmrProcessor.Data;
using GmrProcessor.Data.Gto;
using GmrProcessor.Extensions;
using GmrProcessor.Utils.Mongo;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;
using TestFixtures;

namespace GmrProcessor.Tests.Data;

public class GtoGmrCollectionTests
{
    private readonly Mock<IMongoDbClientFactory> _mockMongoDbClientFactory = new();

    [Fact]
    public async Task UpdateOrInsert_UpdatesOrInsertsAGtoGmr()
    {
        var gmr = GmrFixtures.GmrFixture().Create();
        var gtoGmr = new GtoGmr
        {
            Id = gmr.GmrId,
            Gmr = gmr,
            UpdatedDateTime = gmr.GetUpdatedDateTime(),
        };

        var mockCollection = new Mock<IMongoCollection<GtoGmr>>();

        FilterDefinition<GtoGmr>? filter = null;
        UpdateDefinition<GtoGmr>? update = null;
        FindOneAndUpdateOptions<GtoGmr, GtoGmr>? options = null;

        mockCollection
            .Setup(c =>
                c.FindOneAndUpdateAsync(
                    It.IsAny<FilterDefinition<GtoGmr>>(),
                    It.IsAny<UpdateDefinition<GtoGmr>>(),
                    It.IsAny<FindOneAndUpdateOptions<GtoGmr, GtoGmr>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    FilterDefinition<GtoGmr> f,
                    UpdateDefinition<GtoGmr> u,
                    FindOneAndUpdateOptions<GtoGmr, GtoGmr> o,
                    CancellationToken _
                ) =>
                {
                    filter = f;
                    update = u;
                    options = o;
                }
            );

        _mockMongoDbClientFactory
            .Setup(f => f.GetCollection<GtoGmr>(GtoGmrCollection.CollectionName))
            .Returns(mockCollection.Object);

        var matchedItemCollection = new GtoGmrCollection(_mockMongoDbClientFactory.Object);

        await matchedItemCollection.UpdateOrInsert(gtoGmr, CancellationToken.None);

        options!.IsUpsert.Should().BeTrue();

        var renderArgs = new RenderArgs<GtoGmr>(
            BsonSerializer.SerializerRegistry.GetSerializer<GtoGmr>(),
            BsonSerializer.SerializerRegistry
        );

        var renderedFilter = filter!.Render(renderArgs);
        var expectedFilter = Builders<GtoGmr>.Filter.Where(x => x.Id == gmr.GmrId).Render(renderArgs);

        renderedFilter.Should().BeEquivalentTo(expectedFilter);

        var renderedUpdate = update!.Render(renderArgs).ToString();
        renderedUpdate.Should().Contain("id").And.Contain(gmr.GmrId);
        renderedUpdate.Should().Contain("updatedDateTime");
    }

    [Fact]
    public async Task UpdateOrInsert_ReplacesGmrOnlyWhenIncomingIsNewer()
    {
        var gmr = GmrFixtures.GmrFixture().Create();
        var gtoGmr = new GtoGmr
        {
            Id = gmr.GmrId,
            Gmr = gmr,
            UpdatedDateTime = gmr.GetUpdatedDateTime(),
        };

        var mockCollection = new Mock<IMongoCollection<GtoGmr>>();
        UpdateDefinition<GtoGmr>? capturedUpdate = null;

        mockCollection
            .Setup(c =>
                c.FindOneAndUpdateAsync(
                    It.IsAny<FilterDefinition<GtoGmr>>(),
                    It.IsAny<UpdateDefinition<GtoGmr>>(),
                    It.IsAny<FindOneAndUpdateOptions<GtoGmr, GtoGmr>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    FilterDefinition<GtoGmr> _,
                    UpdateDefinition<GtoGmr> u,
                    FindOneAndUpdateOptions<GtoGmr, GtoGmr> _,
                    CancellationToken _
                ) =>
                {
                    capturedUpdate = u;
                }
            )
            .Returns(Task.FromResult<GtoGmr>(null!));

        _mockMongoDbClientFactory
            .Setup(f => f.GetCollection<GtoGmr>(GtoGmrCollection.CollectionName))
            .Returns(mockCollection.Object);

        var collection = new GtoGmrCollection(_mockMongoDbClientFactory.Object);

        await collection.UpdateOrInsert(gtoGmr, CancellationToken.None);

        var renderArgs = new RenderArgs<GtoGmr>(
            BsonSerializer.SerializerRegistry.GetSerializer<GtoGmr>(),
            BsonSerializer.SerializerRegistry
        );

        var renderedUpdate = capturedUpdate!.Render(renderArgs).ToString();
        renderedUpdate.Should().Contain("\"gmr\" : { \"$cond\"");
        renderedUpdate.Should().Contain("holdStatus");
    }

    [Fact]
    public async Task UpdateOrInsert_PreservesHoldStatusField()
    {
        var gmr = GmrFixtures.GmrFixture().Create();
        var gtoGmr = new GtoGmr
        {
            Id = gmr.GmrId,
            Gmr = gmr,
            UpdatedDateTime = gmr.GetUpdatedDateTime(),
        };

        var mockCollection = new Mock<IMongoCollection<GtoGmr>>();
        UpdateDefinition<GtoGmr>? capturedUpdate = null;

        mockCollection
            .Setup(c =>
                c.FindOneAndUpdateAsync(
                    It.IsAny<FilterDefinition<GtoGmr>>(),
                    It.IsAny<UpdateDefinition<GtoGmr>>(),
                    It.IsAny<FindOneAndUpdateOptions<GtoGmr, GtoGmr>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    FilterDefinition<GtoGmr> _,
                    UpdateDefinition<GtoGmr> u,
                    FindOneAndUpdateOptions<GtoGmr, GtoGmr> _,
                    CancellationToken _
                ) =>
                {
                    capturedUpdate = u;
                }
            )
            .Returns(Task.FromResult<GtoGmr>(null!));

        _mockMongoDbClientFactory
            .Setup(f => f.GetCollection<GtoGmr>(GtoGmrCollection.CollectionName))
            .Returns(mockCollection.Object);

        var collection = new GtoGmrCollection(_mockMongoDbClientFactory.Object);

        await collection.UpdateOrInsert(gtoGmr, CancellationToken.None);

        var renderArgs = new RenderArgs<GtoGmr>(
            BsonSerializer.SerializerRegistry.GetSerializer<GtoGmr>(),
            BsonSerializer.SerializerRegistry
        );

        var renderedUpdate = capturedUpdate!.Render(renderArgs).ToString();
        renderedUpdate.Should().Contain("holdStatus");
        renderedUpdate.Should().Contain("$ifNull");
    }

    [Fact]
    public async Task UpdateHoldStatus_UpdatesHoldFlagForGmr()
    {
        const string gmrId = "GMR123";
        const bool hold = true;

        var mockCollection = new Mock<IMongoCollection<GtoGmr>>();

        FilterDefinition<GtoGmr>? capturedFilter = null;
        UpdateDefinition<GtoGmr>? capturedUpdate = null;
        CancellationToken capturedToken = CancellationToken.None;

        mockCollection
            .Setup(c =>
                c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<GtoGmr>>(),
                    It.IsAny<UpdateDefinition<GtoGmr>>(),
                    It.IsAny<UpdateOptions?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (FilterDefinition<GtoGmr> f, UpdateDefinition<GtoGmr> u, UpdateOptions? _, CancellationToken token) =>
                {
                    capturedFilter = f;
                    capturedUpdate = u;
                    capturedToken = token;
                }
            )
            .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

        _mockMongoDbClientFactory
            .Setup(f => f.GetCollection<GtoGmr>(GtoGmrCollection.CollectionName))
            .Returns(mockCollection.Object);

        var collection = new GtoGmrCollection(_mockMongoDbClientFactory.Object);

        await collection.UpdateHoldStatus(gmrId, hold, CancellationToken.None);

        var renderArgs = new RenderArgs<GtoGmr>(
            BsonSerializer.SerializerRegistry.GetSerializer<GtoGmr>(),
            BsonSerializer.SerializerRegistry
        );

        var renderedFilter = capturedFilter!.Render(renderArgs).ToString();
        renderedFilter.Should().Contain(gmrId);

        var renderedUpdate = capturedUpdate!.Render(renderArgs).ToString();
        renderedUpdate.Should().Contain("holdStatus").And.Contain("true");
        renderedUpdate.Should().Contain("updatedDateTime");

        capturedToken.Should().Be(CancellationToken.None);
    }
}
