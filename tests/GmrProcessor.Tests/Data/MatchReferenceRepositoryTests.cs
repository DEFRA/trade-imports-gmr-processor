using GmrProcessor.Data;
using GmrProcessor.Data.Common;
using GmrProcessor.Data.Gto;
using GmrProcessor.Data.Matching;
using Moq;

namespace GmrProcessor.Tests.Data;

public class MatchReferenceRepositoryTests
{
    private readonly Mock<IMongoContext> _mockMongoContext = new();
    private readonly Mock<IMongoCollectionSet<MrnChedMatch>> _mockMrnChedMatches = new();
    private readonly Mock<IGtoImportTransitCollection> _mockGtoImportTransitCollection = new();
    private readonly MatchReferenceRepository _repository;

    public MatchReferenceRepositoryTests()
    {
        _mockMongoContext.Setup(x => x.MrnChedMatches).Returns(_mockMrnChedMatches.Object);
        _repository = new MatchReferenceRepository(_mockMongoContext.Object, _mockGtoImportTransitCollection.Object);
    }

    [Fact]
    public async Task GetChedsByMrn_ReturnsChedsFromMrnChedMatchOnly()
    {
        var mrn = "24GB12345678901234";
        var chedReferences = new List<string> { "CHEDD.GB.2026.1111111", "CHEDD.GB.2026.2222222" };

        var mrnChedMatch = new MrnChedMatch
        {
            Id = mrn,
            ChedReferences = chedReferences,
            CreatedDateTime = DateTime.UtcNow,
            UpdatedDateTime = DateTime.UtcNow,
        };

        _mockMrnChedMatches
            .Setup(x =>
                x.FindOne(
                    It.IsAny<System.Linq.Expressions.Expression<Func<MrnChedMatch, bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(mrnChedMatch);

        _mockGtoImportTransitCollection
            .Setup(x => x.GetByMrns(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ImportTransit>());

        var result = await _repository.GetChedsByMrn(mrn, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains("CHEDD.GB.2026.1111111", result);
        Assert.Contains("CHEDD.GB.2026.2222222", result);
    }

    [Fact]
    public async Task GetChedsByMrn_ReturnsChedsFromImportTransitOnly()
    {
        var mrn = "24GB12345678901234";

        var importTransits = new List<ImportTransit>
        {
            new()
            {
                Id = "CHEDD.GB.2026.1111111",
                Mrn = mrn,
                TransitOverrideRequired = false,
            },
            new()
            {
                Id = "CHEDD.GB.2026.2222222",
                Mrn = mrn,
                TransitOverrideRequired = false,
            },
        };

        _mockMrnChedMatches
            .Setup(x =>
                x.FindOne(
                    It.IsAny<System.Linq.Expressions.Expression<Func<MrnChedMatch, bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((MrnChedMatch?)null);

        _mockGtoImportTransitCollection
            .Setup(x => x.GetByMrns(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(importTransits);

        var result = await _repository.GetChedsByMrn(mrn, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains("CHEDD.GB.2026.1111111", result);
        Assert.Contains("CHEDD.GB.2026.2222222", result);
    }

    [Fact]
    public async Task GetChedsByMrn_ReturnsChedsFromBothCollections()
    {
        var mrn = "24GB12345678901234";
        var chedReferences = new List<string> { "CHEDD.GB.2026.1111111", "CHEDD.GB.2026.2222222" };

        var mrnChedMatch = new MrnChedMatch
        {
            Id = mrn,
            ChedReferences = chedReferences,
            CreatedDateTime = DateTime.UtcNow,
            UpdatedDateTime = DateTime.UtcNow,
        };

        var importTransits = new List<ImportTransit>
        {
            new()
            {
                Id = "CHEDD.GB.2026.3333333",
                Mrn = mrn,
                TransitOverrideRequired = false,
            },
        };

        _mockMrnChedMatches
            .Setup(x =>
                x.FindOne(
                    It.IsAny<System.Linq.Expressions.Expression<Func<MrnChedMatch, bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(mrnChedMatch);

        _mockGtoImportTransitCollection
            .Setup(x => x.GetByMrns(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(importTransits);

        var result = await _repository.GetChedsByMrn(mrn, CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Contains("CHEDD.GB.2026.1111111", result);
        Assert.Contains("CHEDD.GB.2026.2222222", result);
        Assert.Contains("CHEDD.GB.2026.3333333", result);
    }

    [Fact]
    public async Task GetChedsByMrn_ReturnsEmptyListWhenNoMatchesFound()
    {
        var mrn = "24GB12345678901234";

        _mockMrnChedMatches
            .Setup(x =>
                x.FindOne(
                    It.IsAny<System.Linq.Expressions.Expression<Func<MrnChedMatch, bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((MrnChedMatch?)null);

        _mockGtoImportTransitCollection
            .Setup(x => x.GetByMrns(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ImportTransit>());

        var result = await _repository.GetChedsByMrn(mrn, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetChedsByMrn_DeduplicatesChedsAcrossCollections()
    {
        var mrn = "24GB12345678901234";
        var chedReference = "CHEDD.GB.2026.1111111";

        var mrnChedMatch = new MrnChedMatch
        {
            Id = mrn,
            ChedReferences = new List<string> { chedReference },
            CreatedDateTime = DateTime.UtcNow,
            UpdatedDateTime = DateTime.UtcNow,
        };

        var importTransits = new List<ImportTransit>
        {
            new()
            {
                Id = chedReference,
                Mrn = mrn,
                TransitOverrideRequired = false,
            },
        };

        _mockMrnChedMatches
            .Setup(x =>
                x.FindOne(
                    It.IsAny<System.Linq.Expressions.Expression<Func<MrnChedMatch, bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(mrnChedMatch);

        _mockGtoImportTransitCollection
            .Setup(x => x.GetByMrns(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(importTransits);

        var result = await _repository.GetChedsByMrn(mrn, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(chedReference, result[0]);
    }
}
