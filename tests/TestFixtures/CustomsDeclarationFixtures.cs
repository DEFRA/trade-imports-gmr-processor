using System.Security.Cryptography;
using AutoFixture;
using AutoFixture.Dsl;
using Defra.TradeImportsDataApi.Domain.CustomsDeclaration;
using Defra.TradeImportsDataApi.Domain.Events;

namespace TestFixtures;

public static class CustomsDeclarationFixtures
{
    private const string MrnCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public enum MrnStatus
    {
        NoGmrs = 0,
        NotFinalisable = 1,
        Open = 2,
        CheckedIn = 3,
        Embarked = 4,
        Completed = 5,
        CheckedInInspectionRequired = 6,
        EmbarkedInspectionRequired = 7,
        NotFinalisableAndCheckedIn = 8,
    }

    public static string GenerateMrn(MrnStatus mrnStatus = MrnStatus.Embarked)
    {
        var randomCharacters = new string(
            Enumerable
                .Range(0, 13)
                .Select(_ => MrnCharacters[RandomNumberGenerator.GetInt32(MrnCharacters.Length)])
                .ToArray()
        );

        return $"{DateTime.UtcNow:yy}GB{randomCharacters}{(int)mrnStatus}";
    }

    public static IPostprocessComposer<ResourceEvent<CustomsDeclaration>> CustomsDeclarationResourceEventFixture(
        CustomsDeclaration customsDeclaration
    )
    {
        return GetFixture()
            .Build<ResourceEvent<CustomsDeclaration>>()
            .With(x => x.ResourceId, GenerateMrn())
            .With(x => x.Resource, customsDeclaration)
            .With(x => x.ResourceType, ResourceEventResourceTypes.CustomsDeclaration);
    }

    public static IPostprocessComposer<CustomsDeclaration> CustomsDeclarationFixture()
    {
        return GetFixture()
            .Build<CustomsDeclaration>()
            .With(x => x.ClearanceRequest, ClearanceRequestFixture().Create())
            .With(
                x => x.ClearanceDecision,
                ClearanceDecisionFixture(["CHEDPP.GB.2025.1053368", "CHEDA.GB.2025.1251361"]).Create()
            );
    }

    public static IPostprocessComposer<ClearanceDecision> ClearanceDecisionFixture(List<string> chedReferences)
    {
        var results = chedReferences
            .Select(ched =>
                GetFixture().Build<ClearanceDecisionResult>().With(x => x.ImportPreNotification, ched).Create()
            )
            .ToArray();

        return GetFixture().Build<ClearanceDecision>().With(x => x.Results, results);
    }

    public static IPostprocessComposer<ClearanceRequest> ClearanceRequestFixture()
    {
        return GetFixture().Build<ClearanceRequest>().With(x => x.GoodsLocationCode, "POOPOOPOOGVM");
    }

    private static Fixture GetFixture()
    {
        var fixture = new Fixture();
        fixture.Customize<DateOnly>(o => o.FromFactory((DateTime dt) => DateOnly.FromDateTime(dt)));
        return fixture;
    }
}
