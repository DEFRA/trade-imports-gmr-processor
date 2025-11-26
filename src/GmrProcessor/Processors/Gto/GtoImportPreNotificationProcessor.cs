using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using GmrProcessor.Data;
using MongoDB.Driver;

namespace GmrProcessor.Processors.Gto;

public class GtoImportPreNotificationProcessor(IMongoContext mongoContext) : IGtoImportPreNotificationProcessor
{
    public async Task ProcessAsync(
        ResourceEvent<ImportPreNotification> importPreNotificationEvent,
        CancellationToken cancellationToken
    )
    {
        var reference = importPreNotificationEvent.ResourceId;
        var importPreNotification = importPreNotificationEvent.Resource!;

        var importTransitResult = TransitValidation.IsTransit(importPreNotification);

        if (importTransitResult.IsTransit)
        {
            var transitOverride = TransitOverride.IsTransitOverrideRequired(importPreNotification);

            var filter = Builders<ImportTransit>.Filter.Eq(x => x.Id, reference);
            var update = Builders<ImportTransit>
                .Update.SetOnInsert(x => x.Id, reference)
                .Set(x => x.TransitOverrideRequired, transitOverride.IsOverrideRequired)
                .Set(x => x.Mrn, importTransitResult.Mrn);
            var options = new FindOneAndUpdateOptions<ImportTransit> { IsUpsert = true };

            await mongoContext.ImportTransits.FindOneAndUpdate(filter, update, options, cancellationToken);
        }
    }
}
