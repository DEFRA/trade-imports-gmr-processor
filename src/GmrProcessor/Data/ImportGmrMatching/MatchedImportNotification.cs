using System.Diagnostics.CodeAnalysis;
using MongoDB.Bson.Serialization.Attributes;

namespace GmrProcessor.Data.ImportGmrMatching;

[ExcludeFromCodeCoverage]
public class MatchedImportNotification : IDataEntity
{
    public required string Id { get; set; }

    [BsonElement("mrn")]
    public required string Mrn { get; init; }

    [BsonElement("createdDateTime")]
    public DateTime? CreatedDateTime { get; init; }
}
