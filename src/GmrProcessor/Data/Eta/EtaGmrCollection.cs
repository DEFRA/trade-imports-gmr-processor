using GmrProcessor.Data.Gto;
using GmrProcessor.Utils.Mongo;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GmrProcessor.Data.Eta;

public class EtaGmrCollection(IMongoDbClientFactory database)
    : MongoCollectionSet<EtaGmr>(database, CollectionName),
        IEtaGmrCollection
{
    public const string CollectionName = "EtaGmr";

    public async Task<EtaGmr?> UpdateOrInsert(EtaGmr gmr, CancellationToken cancellationToken)
    {
        var idFilter = Builders<EtaGmr>.Filter.Where(x => x.Id == gmr.Gmr.GmrId);

        var newUpdatedDateTime = gmr.UpdatedDateTime;

        var incomingIsNewer = new BsonDocument(
            "$or",
            new BsonArray
            {
                new BsonDocument("$eq", new BsonArray { "$updatedDateTime", BsonNull.Value }),
                new BsonDocument("$lt", new BsonArray { "$updatedDateTime", newUpdatedDateTime }),
            }
        );

        var updatePipeline = Builders<EtaGmr>.Update.Pipeline(
            new[]
            {
                new BsonDocument(
                    "$set",
                    new BsonDocument
                    {
                        {
                            "gmr",
                            new BsonDocument(
                                "$cond",
                                new BsonArray
                                {
                                    incomingIsNewer,
                                    gmr.Gmr.ToBsonDocument(),
                                    new BsonDocument("$ifNull", new BsonArray { "$gmr", gmr.Gmr.ToBsonDocument() }),
                                }
                            )
                        },
                        {
                            "updatedDateTime",
                            new BsonDocument(
                                "$cond",
                                new BsonArray
                                {
                                    incomingIsNewer,
                                    newUpdatedDateTime,
                                    new BsonDocument(
                                        "$ifNull",
                                        new BsonArray { "$updatedDateTime", newUpdatedDateTime }
                                    ),
                                }
                            )
                        },
                    }
                ),
            }
        );

        return await Collection.FindOneAndUpdateAsync(
            filter: idFilter,
            update: updatePipeline,
            options: new FindOneAndUpdateOptions<EtaGmr> { IsUpsert = true, ReturnDocument = ReturnDocument.Before },
            cancellationToken: cancellationToken
        );
    }
}
