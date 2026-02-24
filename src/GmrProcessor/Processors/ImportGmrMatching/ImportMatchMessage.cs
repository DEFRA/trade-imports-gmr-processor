using System.Text.Json.Serialization;

namespace GmrProcessor.Processors.ImportGmrMatching;

public class ImportMatchMessage
{
    [JsonPropertyName("referenceNumber")]
    public required string ReferenceNumber { get; set; }

    [JsonPropertyName("match")]
    public required bool Match { get; set; }
}
