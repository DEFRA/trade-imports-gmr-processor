using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using GmrProcessor.Utils.Mongo;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace GmrProcessor.Data;

[ExcludeFromCodeCoverage]
public class MongoCollectionSet<T>(IMongoDbClientFactory database, string? collectionName = null)
    : IMongoCollectionSet<T>
    where T : class, IDataEntity
{
    public IMongoCollection<T> Collection => database.GetCollection<T>(collectionName ?? typeof(T).Name);
    private IQueryable<T> Queryable => Collection.AsQueryable();

    public async Task BulkWrite(List<WriteModel<T>> operations, CancellationToken cancellationToken) =>
        await Collection.BulkWriteAsync(operations, new BulkWriteOptions { IsOrdered = false }, cancellationToken);

    public async Task DeleteOneAsync(FilterDefinition<T> filter, CancellationToken cancellationToken) =>
        await Collection.DeleteOneAsync(filter, cancellationToken);

    public async Task<T?> FindOne(Expression<Func<T, bool>> expression, CancellationToken cancellationToken) =>
        await Queryable.SingleOrDefaultAsync(expression, cancellationToken);

    public async Task<List<T>> FindMany<TKey>(
        Expression<Func<T, bool>> where,
        CancellationToken cancellationToken,
        Expression<Func<T, TKey>>? orderBy = null,
        int? limit = null
    )
    {
        var query = Queryable.Where(where);

        if (orderBy is not null)
        {
            query = query.OrderBy(orderBy);
        }
        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<T?> FindOneAndUpdate(
        FilterDefinition<T> filter,
        UpdateDefinition<T> update,
        FindOneAndUpdateOptions<T> options,
        CancellationToken cancellationToken
    )
    {
        return await Collection.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
    }

    public async Task UpdateOne(
        FilterDefinition<T> filter,
        UpdateDefinition<T> update,
        UpdateOptions options,
        CancellationToken cancellationToken
    ) => await Collection.UpdateOneAsync(filter, update, options, cancellationToken);
}
