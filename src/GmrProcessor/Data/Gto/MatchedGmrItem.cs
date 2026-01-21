using System.Diagnostics.CodeAnalysis;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GmrProcessor.Data.Gto;

[ExcludeFromCodeCoverage]
[BsonIgnoreExtraElements]
public class MatchedGmrItem : IDataEntity
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("mrn")]
    public string? Mrn { get; init; }

    [BsonElement("gmrId")]
    public required string GmrId { get; init; }

    [BsonElement("updatedDateTime")]
    public DateTime? UpdatedDateTime { get; init; }
}
