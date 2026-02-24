using System.Text.Json.Serialization;

namespace GmrProcessor.Domain.Eta;

public class IpaffsUpdatedTimeOfArrivalMessage
{
    [JsonPropertyName("referenceNumber")]
    public required string ReferenceNumber { get; init; }

    [JsonPropertyName("entryReference")]
    public required string EntryReference { get; init; }

    [JsonPropertyName("localDateTimeOfArrival")]
    public required DateTime LocalDateTimeOfArrival { get; init; }
}
