using System.Linq.Expressions;
using AutoFixture;
using Defra.TradeImportsGmrFinder.Domain.Events;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract;
using GmrProcessor.Data;
using GmrProcessor.Data.Gto;
using GmrProcessor.Extensions;
using GmrProcessor.Processors.Gto;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;
using TestFixtures;

namespace GmrProcessor.Tests.Processors.Gto;

public class GtoMatchedGmrRepositoryTests
{
    private readonly Mock<IMongoContext> _mongo = new();
    private readonly Mock<IMongoCollectionSet<MatchedGmrItem>> _matchedItems = new();
    private readonly Mock<IGtoGmrCollection> _gtoGmr = new();
    private readonly GtoMatchedGmrRepository _repo;

    public GtoMatchedGmrRepositoryTests()
    {
        _mongo.Setup(m => m.GtoMatchedGmrItem).Returns(_matchedItems.Object);
        _mongo.Setup(m => m.GtoGmr).Returns(_gtoGmr.Object);
        _repo = new GtoMatchedGmrRepository(_mongo.Object);
    }

    [Fact]
    public async Task UpsertMatchedItem_UsesGmrIdAndMrnWithUpsert()
    {
        var matched = new MatchedGmr
        {
            Mrn = CustomsDeclarationFixtures.GenerateMrn(),
            Gmr = GmrFixtures.GmrFixture().Create(),
        };

        FilterDefinition<MatchedGmrItem>? capturedFilter = null;
        UpdateDefinition<MatchedGmrItem>? capturedUpdate = null;
        UpdateOptions? capturedOptions = null;

        _matchedItems
            .Setup(c =>
                c.UpdateOne(
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
                    capturedFilter = f;
                    capturedUpdate = u;
                    capturedOptions = o;
                }
            )
            .Returns(Task.CompletedTask);

        await _repo.UpsertMatchedItem(matched, CancellationToken.None);

        capturedOptions!.IsUpsert.Should().BeTrue();

        var renderArgs = new RenderArgs<MatchedGmrItem>(
            BsonSerializer.SerializerRegistry.GetSerializer<MatchedGmrItem>(),
            BsonSerializer.SerializerRegistry
        );

        var renderedFilter = capturedFilter!.Render(renderArgs).ToString();
        renderedFilter.Should().Contain(matched.Mrn).And.Contain(matched.Gmr.GmrId);

        var renderedUpdate = capturedUpdate!.Render(renderArgs);
        var setDoc = renderedUpdate["$set"].AsBsonDocument;

        setDoc["mrn"].AsString.Should().Be(matched.Mrn);
        setDoc["gmrId"].AsString.Should().Be(matched.Gmr.GmrId);
        setDoc["updatedDateTime"].ToUniversalTime().Should().Be(matched.Gmr.GetUpdatedDateTime());
    }

    [Fact]
    public async Task GetRelatedMrns_FiltersOutNullMrns()
    {
        var mrn1 = ImportPreNotificationFixtures.GenerateRandomReference();
        var mrn2 = ImportPreNotificationFixtures.GenerateRandomReference();
        var gmrId = GmrFixtures.GenerateGmrId();

        var items = new List<MatchedGmrItem>
        {
            new() { GmrId = gmrId, Mrn = mrn1 },
            new() { GmrId = gmrId, Mrn = null },
            new() { GmrId = gmrId, Mrn = mrn2 },
        };

        _matchedItems
            .Setup(m =>
                m.FindMany<MatchedGmrItem>(
                    It.IsAny<Expression<Func<MatchedGmrItem, bool>>>(),
                    It.IsAny<CancellationToken>(),
                    null,
                    null
                )
            )
            .ReturnsAsync(items);

        var result = await _repo.GetRelatedMrns(gmrId, CancellationToken.None);

        result.Should().BeEquivalentTo(new List<string> { mrn1, mrn2 });
    }

    [Fact]
    public async Task UpsertGmr_CallsUpdateOrInsert()
    {
        var gmr = GmrFixtures.GmrFixture().Create();
        var gtoGmr = new GtoGmr
        {
            Id = gmr.GmrId,
            Gmr = gmr,
            UpdatedDateTime = gmr.GetUpdatedDateTime(),
        };
        var persisted = new GtoGmr
        {
            Id = gmr.GmrId,
            Gmr = gmr,
            UpdatedDateTime = gmr.GetUpdatedDateTime(),
            HoldStatus = true,
        };

        _gtoGmr.Setup(g => g.UpdateOrInsert(gtoGmr, It.IsAny<CancellationToken>())).ReturnsAsync(persisted);

        var result = await _repo.UpsertGmr(gtoGmr, CancellationToken.None);

        result.Should().BeSameAs(persisted);
        _gtoGmr.Verify(g => g.UpdateOrInsert(gtoGmr, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByMrn_ReturnsMatchedItemByMrn()
    {
        var mrn = ImportPreNotificationFixtures.GenerateRandomReference();
        var matchedItem = new MatchedGmrItem { Mrn = mrn, GmrId = GmrFixtures.GenerateGmrId() };

        _matchedItems
            .Setup(m =>
                m.FindOne(
                    It.Is<Expression<Func<MatchedGmrItem, bool>>>(f =>
                        f.Compile().Invoke(new MatchedGmrItem { Mrn = mrn, GmrId = matchedItem.GmrId })
                        && !f.Compile().Invoke(new MatchedGmrItem { Mrn = "different", GmrId = matchedItem.GmrId })
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(matchedItem);

        var result = await _repo.GetByMrn(mrn, CancellationToken.None);

        result.Should().BeSameAs(matchedItem);
        _matchedItems.Verify(
            m =>
                m.FindOne(
                    It.Is<Expression<Func<MatchedGmrItem, bool>>>(f =>
                        f.Compile().Invoke(new MatchedGmrItem { Mrn = mrn, GmrId = matchedItem.GmrId })
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}
