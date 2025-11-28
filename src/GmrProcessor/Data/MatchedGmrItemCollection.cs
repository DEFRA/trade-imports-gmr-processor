using GmrProcessor.Extensions;
using GmrProcessor.Utils.Mongo;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GmrProcessor.Data;

public class MatchedGmrItemCollection(IMongoDbClientFactory database, string collectionName)
    : MongoCollectionSet<MatchedGmrItem>(database, collectionName),
        IMatchedGmrItemCollection
{
    public async Task<UpdateResult> UpdateOrInsert(MatchedGmrItem matchedGmrItem, CancellationToken cancellationToken)
    {
        var idFilter = Builders<MatchedGmrItem>.Filter.Where(x =>
            x.ImportTransitId == matchedGmrItem.ImportTransitId
            && x.Mrn == matchedGmrItem.Mrn
            && x.GmrId == matchedGmrItem.Gmr.GmrId
        );

        var newUpdatedDateTime = matchedGmrItem.Gmr.GetUpdatedDateTime();
        var gmrDocument = matchedGmrItem.Gmr.ToBsonDocument();

        var incomingIsNewer = new BsonDocument(
            "$or",
            new BsonArray
            {
                new BsonDocument("$eq", new BsonArray { "$updatedDateTime", BsonNull.Value }),
                new BsonDocument("$lt", new BsonArray { "$updatedDateTime", newUpdatedDateTime }),
            }
        );

        var updatePipeline = Builders<MatchedGmrItem>.Update.Pipeline(
            new[]
            {
                new BsonDocument(
                    "$set",
                    new BsonDocument
                    {
                        { "importTransitId", matchedGmrItem.ImportTransitId },
                        // Assumes Mrn is not null at the moment
                        { "mrn", matchedGmrItem.Mrn },
                        { "gmrId", matchedGmrItem.Gmr.GmrId },
                        {
                            "gmr",
                            new BsonDocument(
                                "$cond",
                                new BsonArray
                                {
                                    incomingIsNewer,
                                    gmrDocument,
                                    new BsonDocument("$ifNull", new BsonArray { "$gmr", gmrDocument }),
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

        return await Collection.UpdateOneAsync(
            filter: idFilter,
            update: updatePipeline,
            options: new UpdateOptions { IsUpsert = true },
            cancellationToken: cancellationToken
        );
    }
}
