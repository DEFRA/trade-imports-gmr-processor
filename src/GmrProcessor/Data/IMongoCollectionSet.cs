using System.Linq.Expressions;
using MongoDB.Driver;

namespace GmrProcessor.Data;

public interface IMongoCollectionSet<T>
    where T : IDataEntity
{
    IMongoCollection<T> Collection { get; }

    Task BulkWrite(List<WriteModel<T>> operations, CancellationToken cancellationToken);

    Task<T?> FindOneAndUpdate(
        FilterDefinition<T> filter,
        UpdateDefinition<T> update,
        FindOneAndUpdateOptions<T> options,
        CancellationToken cancellationToken
    );

    Task<T?> FindOne(Expression<Func<T, bool>> expression, CancellationToken cancellationToken);

    Task<List<T>> FindMany<TKey>(
        Expression<Func<T, bool>> where,
        CancellationToken cancellationToken,
        Expression<Func<T, TKey>>? orderBy = null,
        int? limit = null
    );

    Task Insert(T item, CancellationToken cancellationToken);
}
