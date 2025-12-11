using System.Diagnostics.CodeAnalysis;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract;
using MongoDB.Bson.Serialization.Attributes;

namespace GmrProcessor.Data.Eta;

[ExcludeFromCodeCoverage]
public class EtaGmr : IDataEntity
{
    [BsonElement("_id")]
    public required string Id { get; set; }

    [BsonElement("gmr")]
    public required Gmr Gmr { get; init; }

    [BsonElement("updatedDateTime")]
    public required DateTime UpdatedDateTime { get; init; }
}
