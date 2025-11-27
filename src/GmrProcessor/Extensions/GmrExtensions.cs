using System.Globalization;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract;

namespace GmrProcessor.Extensions;

public static class GmrExtensions
{
    public static DateTime GetUpdatedDateTime(this Gmr gmr)
    {
        return DateTimeOffset
            .Parse(
                gmr.UpdatedDateTime,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
            )
            .UtcDateTime;
    }
}
