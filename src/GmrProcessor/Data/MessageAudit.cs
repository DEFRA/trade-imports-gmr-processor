using System.Diagnostics.CodeAnalysis;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GmrProcessor.Data;

[ExcludeFromCodeCoverage]
public class MessageAudit : IDataEntity
{
    [BsonId]
    [BsonElement("_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("direction")]
    public required string Direction { get; init; }

    [BsonElement("integrationType")]
    public required string IntegrationType { get; init; }

    [BsonElement("target")]
    public required string Target { get; init; }

    [BsonElement("messageBody")]
    public required string MessageBody { get; init; }

    [BsonElement("timestamp")]
    public required DateTime Timestamp { get; init; }

    [BsonElement("messageType")]
    public string? MessageType { get; init; }
}
