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
    public async Task GetEtaByMrn_WhenRecordWithMrnExists_ReturnsMatchedGmr()
    {
        const string mrn = "MRN123";
        var matched = BuildEtaGmrWithCustoms(mrn, "MRN999");
        var nonMatched = BuildEtaGmrWithCustoms("MRN000");

        Expression<Func<EtaGmr, bool>>? capturedPredicate = null;

        _etaGmrCollection
            .Setup(c => c.FindOne(It.IsAny<Expression<Func<EtaGmr, bool>>>(), It.IsAny<CancellationToken>()))
            .Callback((Expression<Func<EtaGmr, bool>> predicate, CancellationToken _) => capturedPredicate = predicate)
            .ReturnsAsync(
                (Expression<Func<EtaGmr, bool>> predicate, CancellationToken _) =>
                    predicate.Compile()(matched) ? matched : null
            );

        var result = await EtaRouteBuilderExtensions.GetEtaByMrn(_etaGmrCollection.Object, mrn, CancellationToken.None);

        capturedPredicate.Should().NotBeNull();
        capturedPredicate!.Compile()(matched).Should().BeTrue();
        capturedPredicate.Compile()(nonMatched).Should().BeFalse();

        var okResult = result.Should().BeOfType<Ok<EtaGmr>>().Subject;
        okResult.Value.Should().Be(matched);
    }

    [Fact]
    public async Task GetEtaByMrn_WhenNoMatchingRecord_ReturnsNotFound()
    {
        const string mrn = "MRN123";
        var nonMatched = BuildEtaGmrWithCustoms("MRN000");

        _etaGmrCollection
            .Setup(c => c.FindOne(It.IsAny<Expression<Func<EtaGmr, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (Expression<Func<EtaGmr, bool>> predicate, CancellationToken _) =>
                    predicate.Compile()(nonMatched) ? nonMatched : null
            );

        var result = await EtaRouteBuilderExtensions.GetEtaByMrn(_etaGmrCollection.Object, mrn, CancellationToken.None);

        result.Should().BeOfType<NotFound>();
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
