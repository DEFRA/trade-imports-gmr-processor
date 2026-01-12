using System.Diagnostics.CodeAnalysis;
using GmrProcessor.Data.Gto;
using GmrProcessor.Utils.Mongo;
using MongoDB.Driver;

namespace GmrProcessor.Data;

[ExcludeFromCodeCoverage]
public class MongoDbInitializerException(Exception inner) : Exception("Failed to initialize mongodb", inner);

[ExcludeFromCodeCoverage]
public class MongoDbInitializer(IMongoDbClientFactory database, ILogger<MongoDbInitializer> logger)
{
    public async Task Init(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Updating Mongo indexes");
        await InitGtoMatchedGmrItemCollection(cancellationToken);
        await InitMessageAuditCollection(cancellationToken);
    }

    private async Task InitGtoMatchedGmrItemCollection(CancellationToken cancellationToken)
    {
        await WithCollectionName<MatchedGmrItem>(GtoMatchedItemCollection.CollectionName)(
            new CreateIndexModel<MatchedGmrItem>(
                Builders<MatchedGmrItem>.IndexKeys.Ascending(x => x.GmrId).Ascending(x => x.Mrn),
                new CreateIndexOptions
                {
                    Name = "UniqueMatch",
                    Background = false,
                    Unique = true,
                }
            ),
            cancellationToken
        );
    }

    private async Task InitMessageAuditCollection(CancellationToken cancellationToken)
    {
        await WithCollectionName<MessageAudit>("MessageAudit")(
            new CreateIndexModel<MessageAudit>(
                Builders<MessageAudit>.IndexKeys.Ascending(x => x.Timestamp),
                new CreateIndexOptions
                {
                    Name = "Timestamp_TTL",
                    Background = true,
                    ExpireAfter = TimeSpan.FromDays(1),
                }
            ),
            cancellationToken
        );

        await WithCollectionName<MessageAudit>("MessageAudit")(
            new CreateIndexModel<MessageAudit>(
                Builders<MessageAudit>
                    .IndexKeys.Ascending(x => x.Timestamp)
                    .Ascending(x => x.Direction)
                    .Ascending(x => x.IntegrationType),
                new CreateIndexOptions { Name = "Timestamp_Direction_IntegrationType", Background = true }
            ),
            cancellationToken
        );

        await WithCollectionName<MessageAudit>("MessageAudit")(
            new CreateIndexModel<MessageAudit>(
                Builders<MessageAudit>.IndexKeys.Ascending(x => x.Target),
                new CreateIndexOptions { Name = "Target_Index", Background = true }
            ),
            cancellationToken
        );
    }

    private Func<CreateIndexModel<T>, CancellationToken, Task> WithCollectionName<T>(string collectionName) =>
        (indexModel, cancellationToken) => CreateIndex(indexModel, collectionName, cancellationToken);

    private async Task CreateIndex<T>(
        CreateIndexModel<T> indexModel,
        string collectionName,
        CancellationToken cancellationToken = default
    )
    {
        var indexName = indexModel.Options.Name;

        try
        {
            logger.LogInformation("Creating index on {IndexName} - {Collection}", indexName, collectionName);

            await database
                .GetCollection<T>(collectionName)
                .Indexes.CreateOneAsync(indexModel, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create index {IndexName} on {Collection}", indexName, collectionName);
            throw new MongoDbInitializerException(e);
        }
    }
}
