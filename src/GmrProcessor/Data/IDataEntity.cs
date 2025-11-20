using MongoDB.Bson.Serialization.Attributes;

namespace GmrProcessor.Data;

public interface IDataEntity
{
    [BsonId]
    string Id { get; set; }
}
