using AutoFixture;
using AutoFixture.Dsl;
using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;

namespace TestFixtures;

public static class ImportPreNotificationFixtures
{
    public static IPostprocessComposer<ResourceEvent<ImportPreNotification>> ImportPreNotificationResourceEventFixture(
        ImportPreNotification importPreNotification
    ) =>
        GetFixture()
            .Build<ResourceEvent<ImportPreNotification>>()
            .With(x => x.Resource, importPreNotification)
            .With(x => x.ResourceId, "CHEDPP.GB.2025.1053368")
            .With(x => x.ResourceType, ResourceEventResourceTypes.ImportPreNotification);

    public static IPostprocessComposer<ImportPreNotification> ImportPreNotificationFixture(string? transitMrn = null)
    {
        var importPreNotification = GetFixture().Build<ImportPreNotification>();
        if (transitMrn == null)
        {
            return importPreNotification;
        }

        return importPreNotification.With(
            x => x.ExternalReferences,
            [new ExternalReference { Reference = transitMrn, System = "NCTS" }]
        );
    }

    private static Fixture GetFixture()
    {
        var fixture = new Fixture();
        fixture.Customize<DateOnly>(o => o.FromFactory((DateTime dt) => DateOnly.FromDateTime(dt)));
        return fixture;
    }

    public static string GenerateRandomReference()
    {
        var chedTypes = new[] { "CHEDP", "CHEDPP", "CHEDA", "CHEDD" };
        var random = new Random();
        var chedType = chedTypes[random.Next(chedTypes.Length)];
        var year = DateTime.UtcNow.Year;
        var number = random.Next(0, 10000000).ToString("D7");
        return $"{chedType}.GB.{year}.{number}";
    }
}
