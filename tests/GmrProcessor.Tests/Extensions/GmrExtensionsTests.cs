using AutoFixture;
using GmrProcessor.Extensions;
using TestFixtures;

namespace GmrProcessor.Tests.Extensions;

public class GmrExtensionsTests
{
    [Fact]
    public void GetUpdatedDateTime_ParsesUpdatedDateTimeStringToDateTime()
    {
        var gmr = GmrFixtures.GmrFixture().With(g => g.UpdatedDateTime, "2024-10-15T10:00:00+02:00").Create();

        var result = gmr.GetUpdatedDateTime();

        result.Should().Be(new DateTime(2024, 10, 15, 8, 0, 0, DateTimeKind.Utc));
        result.Kind.Should().Be(DateTimeKind.Utc);
    }
}
