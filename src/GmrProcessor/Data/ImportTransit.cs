using System.Diagnostics.CodeAnalysis;

namespace GmrProcessor.Data;

[ExcludeFromCodeCoverage]
public class ImportTransit : IDataEntity
{
    public required string Id { get; set; }
    public required bool TransitOverrideRequired { get; init; }
    public string? Mrn { get; init; }
}
