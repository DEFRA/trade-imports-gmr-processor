using System.Diagnostics.CodeAnalysis;
using GmrProcessor.Utils.Mongo;

namespace GmrProcessor.Data.Gto;

[ExcludeFromCodeCoverage]
public class GtoMatchedItemCollection(IMongoDbClientFactory database)
    : MongoCollectionSet<MatchedGmrItem>(database, CollectionName)
{
    public const string CollectionName = "GtoMatchedGmrItem";
}
