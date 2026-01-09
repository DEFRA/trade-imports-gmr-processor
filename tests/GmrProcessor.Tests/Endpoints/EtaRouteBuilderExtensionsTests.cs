using System.Linq.Expressions;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract;
using GmrProcessor.Data.Eta;
using GmrProcessor.Endpoints;
using GmrProcessor.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using TestFixtures;

namespace GmrProcessor.Tests.Endpoints;

public class EtaRouteBuilderExtensionsTests
{
    private readonly Mock<IEtaGmrCollection> _etaGmrCollection = new();

    [Fact]
    public async Task GetEtaByMrn_WhenRecordsWithMrnExist_ReturnsMatchedGmrs()
    {
        const string mrn = "MRN123";
        var matched = BuildEtaGmrWithCustoms(mrn, "MRN999");
        var matchedSecond = BuildEtaGmrWithCustoms(mrn, "MRN777");

        _etaGmrCollection
            .Setup(c =>
                c.FindMany(
                    It.IsAny<Expression<Func<EtaGmr, bool>>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Expression<Func<EtaGmr, EtaGmr>>?>(),
                    It.IsAny<int?>()
                )
            )
            .ReturnsAsync([matched, matchedSecond]);

        var result = await EtaRouteBuilderExtensions.GetEtaByMrn(_etaGmrCollection.Object, mrn, CancellationToken.None);

        var okResult = result.Should().BeOfType<Ok<List<EtaGmr>>>().Subject;
        okResult.Value.Should().BeEquivalentTo([matched, matchedSecond]);
    }

    [Fact]
    public async Task GetEtaByMrn_WhenNoMatchingRecord_ReturnsEmptyList()
    {
        const string mrn = "MRN123";

        _etaGmrCollection
            .Setup(c =>
                c.FindMany(
                    It.IsAny<Expression<Func<EtaGmr, bool>>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Expression<Func<EtaGmr, EtaGmr>>?>(),
                    It.IsAny<int?>()
                )
            )
            .ReturnsAsync([]);

        var result = await EtaRouteBuilderExtensions.GetEtaByMrn(_etaGmrCollection.Object, mrn, CancellationToken.None);

        var okResult = result.Should().BeOfType<Ok<List<EtaGmr>>>().Subject;
        okResult.Value.Should().BeEmpty();
    }

    private static EtaGmr BuildEtaGmrWithCustoms(params string[] mrns)
    {
        var gmr = GmrFixtures
            .GmrFixture()
            .With(
                g => g.Declarations,
                new GmrDeclarationResponse
                {
                    Customs = mrns.Select(mrn => new GmrDeclarationEntityResponse { Id = mrn }).ToList(),
                }
            )
            .Create();

        return new EtaGmr
        {
            Id = gmr.GmrId,
            Gmr = gmr,
            UpdatedDateTime = gmr.GetUpdatedDateTime(),
        };
    }
}
