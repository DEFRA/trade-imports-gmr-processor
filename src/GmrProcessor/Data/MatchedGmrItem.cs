using Defra.TradeImportsGmrFinder.GvmsClient.Contract;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GmrProcessor.Data;

public class MatchedGmrItem : IDataEntity
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public required string ImportTransitId { get; init; }
    public string? Mrn { get; init; }
    public required string GmrId { get; init; }
    public required Gmr Gmr { get; init; }
    public DateTime UpdatedDateTime { get; init; }
}
