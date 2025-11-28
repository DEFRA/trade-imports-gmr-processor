using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using AutoFixture;
using AutoFixture.Dsl;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract;

namespace TestFixtures;

public static class GmrFixtures
{
    private const string DateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public static IPostprocessComposer<Gmr> GmrFixture()
    {
        return GetFixture().Build<Gmr>().With(g => g.GmrId, GenerateGmrId()).WithDateTime(DateTimeOffset.UtcNow);
    }

    public static IPostprocessComposer<Gmr> WithDateTime(this IPostprocessComposer<Gmr> gmr, DateTimeOffset dateTime)
    {
        return gmr.With(g => g.UpdatedDateTime, dateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture));
    }

    public static string GenerateGmrId()
    {
        const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string alphanumerics = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        var letter = letters[RandomNumberGenerator.GetInt32(letters.Length)];
        var reference = new string(
            Enumerable
                .Range(0, 8)
                .Select(_ => alphanumerics[RandomNumberGenerator.GetInt32(alphanumerics.Length)])
                .ToArray()
        );

        return $"GMR{letter}{reference}";
    }

    private static Fixture GetFixture()
    {
        var fixture = new Fixture();
        fixture.Customize<DateOnly>(o => o.FromFactory((DateTime dt) => DateOnly.FromDateTime(dt)));
        return fixture;
    }
}
