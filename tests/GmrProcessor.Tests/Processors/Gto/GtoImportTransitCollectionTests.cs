using System.Linq.Expressions;
using Defra.TradeImportsDataApi.Domain.Gvms;
using GmrProcessor.Data;
using GmrProcessor.Data.Common;
using GmrProcessor.Data.Gto;
using GmrProcessor.Processors.Gto;
using Moq;
using TestFixtures;

namespace GmrProcessor.Tests.Processors.Gto;

public class GtoImportTransitCollectionTests
{
    private readonly Mock<IMongoContext> _mongo = new();
    private readonly Mock<IMongoCollectionSet<ImportTransit>> _mockImportTransits = new();
    private readonly GtoImportTransitCollection _repo;

    public GtoImportTransitCollectionTests()
    {
        _mongo.Setup(m => m.ImportTransits).Returns(_mockImportTransits.Object);
        _repo = new GtoImportTransitCollection(_mongo.Object);
    }

    [Fact]
    public async Task GetByMrn_CallsFindOne()
    {
        var expected = new ImportTransit
        {
            Id = ImportPreNotificationFixtures.GenerateRandomReference(),
            Mrn = CustomsDeclarationFixtures.GenerateMrn(),
            TransitOverrideRequired = true,
        };

        _mockImportTransits
            .Setup(m => m.FindOne(It.IsAny<Expression<Func<ImportTransit, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _repo.GetByMrn(expected.Id, CancellationToken.None);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetByMrns_CallsFindMany()
    {
        var mrn1 = CustomsDeclarationFixtures.GenerateMrn();
        var mrn2 = CustomsDeclarationFixtures.GenerateMrn();

        var results = new List<ImportTransit>
        {
            new()
            {
                Id = GmrFixtures.GenerateGmrId(),
                Mrn = mrn1,
                TransitOverrideRequired = false,
            },
            new()
            {
                Id = GmrFixtures.GenerateGmrId(),
                Mrn = mrn2,
                TransitOverrideRequired = true,
            },
        };

        _mockImportTransits
            .Setup(m =>
                m.FindMany<object>(
                    It.IsAny<Expression<Func<ImportTransit, bool>>>(),
                    It.IsAny<CancellationToken>(),
                    null,
                    null
                )
            )
            .ReturnsAsync(results);

        var result = await _repo.GetByMrns([mrn1, mrn2], CancellationToken.None);

        result.Should().BeEquivalentTo(results);
    }
}
