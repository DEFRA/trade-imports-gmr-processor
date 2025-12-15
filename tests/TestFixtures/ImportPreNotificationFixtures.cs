using System.Security.Cryptography;
using AutoFixture;
using AutoFixture.Dsl;
using Defra.TradeImportsDataApi.Api.Client;
using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;

namespace TestFixtures;

public static class ImportPreNotificationFixtures
{
    public static IPostprocessComposer<ImportPreNotificationResponse> ImportPreNotificationResponseFixture(
        ImportPreNotification importPreNotification
    ) => GetFixture().Build<ImportPreNotificationResponse>().With(x => x.ImportPreNotification, importPreNotification);

    public static IPostprocessComposer<ResourceEvent<ImportPreNotification>> ImportPreNotificationResourceEventFixture(
        ImportPreNotification importPreNotification
    ) =>
        GetFixture()
            .Build<ResourceEvent<ImportPreNotification>>()
            .With(x => x.Resource, importPreNotification)
            .With(x => x.ResourceId, importPreNotification.ReferenceNumber)
            .With(x => x.ResourceType, ResourceEventResourceTypes.ImportPreNotification);

    public static IPostprocessComposer<ImportPreNotification> ImportPreNotificationFixture(string reference) =>
        GetFixture().Build<ImportPreNotification>().With(x => x.ReferenceNumber, reference);

    public static IPostprocessComposer<ImportPreNotification> WithMrn(
        this IPostprocessComposer<ImportPreNotification> notification,
        string mrn
    ) => notification.With(x => x.ExternalReferences, [new ExternalReference { Reference = mrn, System = "NCTS" }]);

    private static Fixture GetFixture()
    {
        var fixture = new Fixture();
        fixture.Customize<DateOnly>(o => o.FromFactory((DateTime dt) => DateOnly.FromDateTime(dt)));
        return fixture;
    }

    public static string GenerateRandomReference()
    {
        var chedTypes = new[] { "CHEDP", "CHEDPP", "CHEDA", "CHEDD" };
        var chedType = chedTypes[RandomNumberGenerator.GetInt32(chedTypes.Length)];
        var year = DateTime.UtcNow.Year;
        var number = RandomNumberGenerator.GetInt32(0, 10000000).ToString("D7");
        return $"{chedType}.GB.{year}.{number}";
    }
}
