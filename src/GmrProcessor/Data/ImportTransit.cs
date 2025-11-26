namespace GmrProcessor.Data;

public class ImportTransit : IDataEntity
{
    public required string Id { get; set; }

    public required bool TransitOverrideRequired { get; set; }
    public string? DeclarationId { get; set; }
}