using System.Diagnostics.CodeAnalysis;

namespace GmrProcessor.Data;

[ExcludeFromCodeCoverage]
public class MatchedImportNotification : IDataEntity
{
    public required string Id { get; set; }
    public required string Mrn { get; init; }
}
