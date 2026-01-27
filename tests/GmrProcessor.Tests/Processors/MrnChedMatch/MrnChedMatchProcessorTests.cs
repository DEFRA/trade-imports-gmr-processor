using Defra.TradeImportsDataApi.Domain.CustomsDeclaration;
using GmrProcessor.Data;
using GmrProcessor.Processors.MrnChedMatch;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Moq;
using TestFixtures;

namespace GmrProcessor.Tests.Processors.MrnChedMatch;

public class MrnChedMatchProcessorTests
{
    private readonly Mock<IMongoContext> _mockMongoContext = new();
    private readonly Mock<IMongoCollectionSet<GmrProcessor.Data.Matching.MrnChedMatch>> _mockMrnChedMatches = new();
    private readonly MrnChedMatchProcessor _processor;

    public MrnChedMatchProcessorTests()
    {
        _mockMongoContext.Setup(x => x.MrnChedMatches).Returns(_mockMrnChedMatches.Object);
        _processor = new MrnChedMatchProcessor(_mockMongoContext.Object, NullLogger<MrnChedMatchProcessor>.Instance);
    }

    [Fact]
    public async Task ProcessCustomsDeclaration_CreatesManyMatchesWhenValid()
    {
        var chedReferences = new List<string> { "CHEDD.GB.2026.1234567", "CHEDA.GB.2026.7654321" };
        var customsDeclaration = CustomsDeclarationFixtures
            .CustomsDeclarationFixture()
            .With(
                x => x.ClearanceDecision,
                CustomsDeclarationFixtures.ClearanceDecisionFixture(chedReferences).Create()
            )
            .Create();
        var resourceEvent = CustomsDeclarationFixtures
            .CustomsDeclarationResourceEventFixture(customsDeclaration)
            .Create();

        _mockMrnChedMatches
            .Setup(x =>
                x.FindOne(
                    It.IsAny<System.Linq.Expressions.Expression<Func<GmrProcessor.Data.Matching.MrnChedMatch, bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((GmrProcessor.Data.Matching.MrnChedMatch?)null);

        var result = await _processor.ProcessCustomsDeclaration(resourceEvent, CancellationToken.None);

        Assert.Equal(MrnChedMatchProcessorResult.MatchCreated, result);
        _mockMrnChedMatches.Verify(
            x =>
                x.ReplaceOne(
                    It.IsAny<System.Linq.Expressions.Expression<Func<GmrProcessor.Data.Matching.MrnChedMatch, bool>>>(),
                    It.IsAny<GmrProcessor.Data.Matching.MrnChedMatch>(),
                    It.Is<ReplaceOptions>(o => o.IsUpsert),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task ProcessCustomsDeclaration_SkipsWhenNoChedReferences()
    {
        var customsDeclaration = CustomsDeclarationFixtures
            .CustomsDeclarationFixture()
            .With(x => x.ClearanceDecision, CustomsDeclarationFixtures.ClearanceDecisionFixture([]).Create())
            .Create();
        var resourceEvent = CustomsDeclarationFixtures
            .CustomsDeclarationResourceEventFixture(customsDeclaration)
            .Create();

        var result = await _processor.ProcessCustomsDeclaration(resourceEvent, CancellationToken.None);

        Assert.Equal(MrnChedMatchProcessorResult.SkippedNoChedReferences, result);
        _mockMrnChedMatches.Verify(
            x =>
                x.BulkWrite(
                    It.IsAny<List<WriteModel<GmrProcessor.Data.Matching.MrnChedMatch>>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task ProcessCustomsDeclaration_SkipsWhenClearanceDecisionIsNull()
    {
        var customsDeclaration = CustomsDeclarationFixtures
            .CustomsDeclarationFixture()
            .With(x => x.ClearanceDecision, (ClearanceDecision?)null)
            .Create();
        var resourceEvent = CustomsDeclarationFixtures
            .CustomsDeclarationResourceEventFixture(customsDeclaration)
            .Create();

        var result = await _processor.ProcessCustomsDeclaration(resourceEvent, CancellationToken.None);

        Assert.Equal(MrnChedMatchProcessorResult.SkippedNoChedReferences, result);
        _mockMrnChedMatches.Verify(
            x =>
                x.BulkWrite(
                    It.IsAny<List<WriteModel<GmrProcessor.Data.Matching.MrnChedMatch>>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task ProcessCustomsDeclaration_SkipsWhenInvalidMrn()
    {
        var chedReferences = new List<string> { "CHEDD.GB.2026.1234567" };
        var customsDeclaration = CustomsDeclarationFixtures
            .CustomsDeclarationFixture()
            .With(
                x => x.ClearanceDecision,
                CustomsDeclarationFixtures.ClearanceDecisionFixture(chedReferences).Create()
            )
            .Create();
        var resourceEvent = CustomsDeclarationFixtures
            .CustomsDeclarationResourceEventFixture(customsDeclaration)
            .With(x => x.ResourceId, "INVALID_MRN")
            .Create();

        var result = await _processor.ProcessCustomsDeclaration(resourceEvent, CancellationToken.None);

        Assert.Equal(MrnChedMatchProcessorResult.SkippedInvalidMrn, result);
        _mockMrnChedMatches.Verify(
            x =>
                x.BulkWrite(
                    It.IsAny<List<WriteModel<GmrProcessor.Data.Matching.MrnChedMatch>>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task ProcessCustomsDeclaration_FiltersDuplicateChedReferences()
    {
        var chedReferences = new List<string>
        {
            "CHEDD.GB.2026.1234567",
            "CHEDA.GB.2026.7654321",
            "CHEDD.GB.2026.1234567", // Duplicate
        };
        var customsDeclaration = CustomsDeclarationFixtures
            .CustomsDeclarationFixture()
            .With(
                x => x.ClearanceDecision,
                CustomsDeclarationFixtures.ClearanceDecisionFixture(chedReferences).Create()
            )
            .Create();
        var resourceEvent = CustomsDeclarationFixtures
            .CustomsDeclarationResourceEventFixture(customsDeclaration)
            .Create();

        _mockMrnChedMatches
            .Setup(x =>
                x.FindOne(
                    It.IsAny<System.Linq.Expressions.Expression<Func<GmrProcessor.Data.Matching.MrnChedMatch, bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((GmrProcessor.Data.Matching.MrnChedMatch?)null);

        var result = await _processor.ProcessCustomsDeclaration(resourceEvent, CancellationToken.None);

        Assert.Equal(MrnChedMatchProcessorResult.MatchCreated, result);
        _mockMrnChedMatches.Verify(
            x =>
                x.ReplaceOne(
                    It.IsAny<System.Linq.Expressions.Expression<Func<GmrProcessor.Data.Matching.MrnChedMatch, bool>>>(),
                    It.IsAny<GmrProcessor.Data.Matching.MrnChedMatch>(),
                    It.Is<ReplaceOptions>(o => o.IsUpsert),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}
