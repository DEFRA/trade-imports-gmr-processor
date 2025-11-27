using AutoFixture;
using FluentAssertions;
using GmrProcessor.Data;
using GmrProcessor.Utils.Mongo;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;
using TestFixtures;

namespace GmrProcessor.Tests.Data;

public class MatchedGmrItemCollectionTests
{
    private readonly Mock<IMongoDbClientFactory> _mockMongoDbClientFactory = new();
    private const string CollectionName = "MatchedGmrItems";

    [Fact]
    public async Task UpdateOrInsert_UpdatesOrInsertsAMatchedGmrItem()
    {
        var gmrId = GmrFixtures.GenerateGmrId();
        var gmr = GmrFixtures.GmrFixture().With(g => g.GmrId, gmrId).Create();

        var matchedGmrItem = new MatchedGmrItem
        {
            ImportTransitId = ImportPreNotificationFixtures.GenerateRandomReference(),
            Mrn = CustomsDeclarationFixtures.GenerateMrn(),
            GmrId = gmrId,
            Gmr = gmr,
        };

        var mockCollection = new Mock<IMongoCollection<MatchedGmrItem>>();

        FilterDefinition<MatchedGmrItem>? filter = null;
        UpdateDefinition<MatchedGmrItem>? update = null;
        UpdateOptions? options = null;

        mockCollection
            .Setup(c =>
                c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<MatchedGmrItem>>(),
                    It.IsAny<UpdateDefinition<MatchedGmrItem>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    FilterDefinition<MatchedGmrItem> f,
                    UpdateDefinition<MatchedGmrItem> u,
                    UpdateOptions o,
                    CancellationToken _
                ) =>
                {
                    filter = f;
                    update = u;
                    options = o;
                }
            );

        _mockMongoDbClientFactory
            .Setup(f => f.GetCollection<MatchedGmrItem>(CollectionName))
            .Returns(mockCollection.Object);

        var matchedItemCollection = new MatchedGmrItemCollection(_mockMongoDbClientFactory.Object, CollectionName);

        await matchedItemCollection.UpdateOrInsert(matchedGmrItem, CancellationToken.None);

        options!.IsUpsert.Should().BeTrue();

        var renderArgs = new RenderArgs<MatchedGmrItem>(
            BsonSerializer.SerializerRegistry.GetSerializer<MatchedGmrItem>(),
            BsonSerializer.SerializerRegistry
        );

        var renderedFilter = filter!.Render(renderArgs);
        var expectedFilter = Builders<MatchedGmrItem>
            .Filter.Where(x =>
                x.ImportTransitId == matchedGmrItem.ImportTransitId
                && x.Mrn == matchedGmrItem.Mrn
                && x.GmrId == matchedGmrItem.Gmr.GmrId
            )
            .Render(renderArgs);

        renderedFilter.Should().BeEquivalentTo(expectedFilter);

        var renderedUpdate = update!.Render(renderArgs).ToString();
        renderedUpdate.Should().Contain("importTransitId").And.Contain(matchedGmrItem.ImportTransitId);
        renderedUpdate.Should().Contain("mrn").And.Contain(matchedGmrItem.Mrn);
        renderedUpdate.Should().Contain("gmrId").And.Contain(matchedGmrItem.GmrId);
        renderedUpdate.Should().Contain("updatedDateTime");

        mockCollection.Verify(
            c =>
                c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<MatchedGmrItem>>(),
                    It.IsAny<UpdateDefinition<MatchedGmrItem>>(),
                    It.Is<UpdateOptions>(o => o.IsUpsert),
                    CancellationToken.None
                ),
            Times.Once
        );
    }
}
