using System.Diagnostics.CodeAnalysis;
using MongoDB.Bson.Serialization.Attributes;

namespace GmrProcessor.Data.Matching;

[ExcludeFromCodeCoverage]
public class MrnChedMatch : IDataEntity
{
    [BsonId]
    [BsonElement("_id")]
    public required string Id { get; set; }

    [BsonElement("chedReferences")]
    public required List<string> ChedReferences { get; set; }

    [BsonElement("createdDateTime")]
    public DateTime CreatedDateTime { get; init; }

    [BsonElement("updatedDateTime")]
    public DateTime UpdatedDateTime { get; set; }
}
