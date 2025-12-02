using System.Diagnostics.CodeAnalysis;
using GmrProcessor.Utils.Mongo;

namespace GmrProcessor.Data;

[ExcludeFromCodeCoverage]
public class GtoMatchedGmrItemCollection(IMongoDbClientFactory database)
    : MatchedGmrItemCollection(database, CollectionName)
{
    private const string CollectionName = "GtoMatchedGmrItem";
}
