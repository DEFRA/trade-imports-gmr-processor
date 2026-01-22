using System.Diagnostics.CodeAnalysis;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract;
using MongoDB.Bson.Serialization.Attributes;

namespace GmrProcessor.Data.Gto;

[ExcludeFromCodeCoverage]
public class GtoGmr : IDataEntity
{
    [BsonElement("_id")]
    public required string Id { get; set; }

    [BsonElement("gmr")]
    public required Gmr Gmr { get; init; }

    [BsonElement("holdStatus")]
    public bool? HoldStatus { get; init; }

    [BsonElement("updatedDateTime")]
    public required DateTime UpdatedDateTime { get; init; }
}
