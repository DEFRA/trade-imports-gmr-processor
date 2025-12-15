using GmrProcessor.Utils.Mongo;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GmrProcessor.Data.Gto;

public class GtoGmrCollection(IMongoDbClientFactory database)
    : MongoCollectionSet<GtoGmr>(database, CollectionName),
        IGtoGmrCollection
{
    public const string CollectionName = "GtoGmr";

    public async Task<GtoGmr> UpdateOrInsert(GtoGmr gmr, CancellationToken cancellationToken)
    {
        var idFilter = Builders<GtoGmr>.Filter.Where(x => x.Id == gmr.Gmr.GmrId);

        var newUpdatedDateTime = gmr.UpdatedDateTime;

        var incomingIsNewer = new BsonDocument(
            "$or",
            new BsonArray
            {
                new BsonDocument("$eq", new BsonArray { "$updatedDateTime", BsonNull.Value }),
                new BsonDocument("$lt", new BsonArray { "$updatedDateTime", newUpdatedDateTime }),
            }
        );

        var updatePipeline = Builders<GtoGmr>.Update.Pipeline(
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
            options: new FindOneAndUpdateOptions<GtoGmr> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
            cancellationToken: cancellationToken
        );
    }

    public async Task UpdateHoldStatus(string gmrId, bool holdStatus, CancellationToken cancellationToken)
    {
        var filter = Builders<GtoGmr>.Filter.Where(x => x.Id == gmrId);
        var update = Builders<GtoGmr>.Update.Set(x => x.HoldStatus, holdStatus);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }
}
