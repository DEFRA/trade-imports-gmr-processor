using GmrProcessor.Data.Eta;
using GmrProcessor.Extensions;
using GmrProcessor.Utils.Mongo;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;
using TestFixtures;

namespace GmrProcessor.Tests.Data;

public class EtaGmrCollectionTests
{
    private readonly Mock<IMongoDbClientFactory> _mongoFactory = new();

    [Fact]
    public async Task UpdateOrInsert_UpsertsEtaGmrAndReturnsPreviousDocument()
    {
        var gmr = GmrFixtures.GmrFixture().Create();
        var etaGmr = new EtaGmr
        {
            Id = gmr.GmrId,
            Gmr = gmr,
            UpdatedDateTime = gmr.GetUpdatedDateTime(),
        };

        var mockCollection = new Mock<IMongoCollection<EtaGmr>>();
        FilterDefinition<EtaGmr>? capturedFilter = null;
        UpdateDefinition<EtaGmr>? capturedUpdate = null;
        FindOneAndUpdateOptions<EtaGmr, EtaGmr>? capturedOptions = null;

        var previous = new EtaGmr
        {
            Id = gmr.GmrId,
            Gmr = gmr,
            UpdatedDateTime = gmr.GetUpdatedDateTime().AddMinutes(-5),
        };

        mockCollection
            .Setup(c =>
                c.FindOneAndUpdateAsync(
                    It.IsAny<FilterDefinition<EtaGmr>>(),
                    It.IsAny<UpdateDefinition<EtaGmr>>(),
                    It.IsAny<FindOneAndUpdateOptions<EtaGmr, EtaGmr>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    FilterDefinition<EtaGmr> f,
                    UpdateDefinition<EtaGmr> u,
                    FindOneAndUpdateOptions<EtaGmr, EtaGmr> o,
                    CancellationToken _
                ) =>
                {
                    capturedFilter = f;
                    capturedUpdate = u;
                    capturedOptions = o;
                }
            )
            .ReturnsAsync(previous);

        _mongoFactory
            .Setup(f => f.GetCollection<EtaGmr>(EtaGmrCollection.CollectionName))
            .Returns(mockCollection.Object);

        var collection = new EtaGmrCollection(_mongoFactory.Object);

        var result = await collection.UpdateOrInsert(etaGmr, CancellationToken.None);

        result.Should().Be(previous);
        capturedOptions!.IsUpsert.Should().BeTrue();
        capturedOptions.ReturnDocument.Should().Be(ReturnDocument.Before);

        var renderArgs = new RenderArgs<EtaGmr>(
            BsonSerializer.SerializerRegistry.GetSerializer<EtaGmr>(),
            BsonSerializer.SerializerRegistry
        );

        var renderedFilter = capturedFilter!.Render(renderArgs);
        var expectedFilter = Builders<EtaGmr>.Filter.Where(x => x.Id == gmr.GmrId).Render(renderArgs);
        renderedFilter.Should().BeEquivalentTo(expectedFilter);

        var renderedUpdate = capturedUpdate!.Render(renderArgs).ToString();
        renderedUpdate.Should().Contain("updatedDateTime");
        renderedUpdate.Should().Contain("gmr");
    }

    [Fact]
    public async Task UpdateOrInsert_ReplacesGmrOnlyWhenIncomingIsNewer()
    {
        var gmr = GmrFixtures.GmrFixture().Create();
        var etaGmr = new EtaGmr
        {
            Id = gmr.GmrId,
            Gmr = gmr,
            UpdatedDateTime = gmr.GetUpdatedDateTime(),
        };

        var mockCollection = new Mock<IMongoCollection<EtaGmr>>();
        UpdateDefinition<EtaGmr>? capturedUpdate = null;

        mockCollection
            .Setup(c =>
                c.FindOneAndUpdateAsync(
                    It.IsAny<FilterDefinition<EtaGmr>>(),
                    It.IsAny<UpdateDefinition<EtaGmr>>(),
                    It.IsAny<FindOneAndUpdateOptions<EtaGmr, EtaGmr>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    FilterDefinition<EtaGmr> _,
                    UpdateDefinition<EtaGmr> u,
                    FindOneAndUpdateOptions<EtaGmr, EtaGmr> _,
                    CancellationToken _
                ) =>
                {
                    capturedUpdate = u;
                }
            )
            .Returns(Task.FromResult<EtaGmr>(null!));

        _mongoFactory
            .Setup(f => f.GetCollection<EtaGmr>(EtaGmrCollection.CollectionName))
            .Returns(mockCollection.Object);

        var collection = new EtaGmrCollection(_mongoFactory.Object);

        await collection.UpdateOrInsert(etaGmr, CancellationToken.None);

        var renderArgs = new RenderArgs<EtaGmr>(
            BsonSerializer.SerializerRegistry.GetSerializer<EtaGmr>(),
            BsonSerializer.SerializerRegistry
        );

        var renderedUpdate = capturedUpdate!.Render(renderArgs).ToString();

        renderedUpdate.Should().Contain("\"gmr\" : { \"$cond\"");
    }
}
