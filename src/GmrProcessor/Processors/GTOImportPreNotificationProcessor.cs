using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using GmrProcessor.Data;
using MongoDB.Driver;

namespace GmrProcessor.Processors;

public class GtoImportPreNotificationProcessor(IMongoContext mongoContext) : IGtoImportPreNotificationProcessor
{
    public async Task ProcessAsync(
        ResourceEvent<ImportPreNotification> importPreNotification,
        CancellationToken cancellationToken
    )
    {
        var filter = Builders<ImportTransit>.Filter.Eq(x => x.Id, importPreNotification.ResourceId);
        var update = Builders<ImportTransit>.Update.SetOnInsert(x => x.Id, importPreNotification.ResourceId);
        var options = new FindOneAndUpdateOptions<ImportTransit> { IsUpsert = true };

        await mongoContext.ImportTransits.FindOneAndUpdate(filter, update, options, cancellationToken);
    }
}
