using System.Text.Json.Serialization;

namespace GmrProcessor.Processors.ImportGmrMatching;

public class ImportMatchMessage
{
    [JsonPropertyName("importReference")]
    public required string ImportReference { get; set; }

    [JsonPropertyName("match")]
    public required bool Match { get; set; }
}
