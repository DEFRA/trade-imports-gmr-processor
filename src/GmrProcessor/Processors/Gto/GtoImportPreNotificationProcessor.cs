using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using GmrProcessor.Data;
using MongoDB.Driver;

namespace GmrProcessor.Processors.Gto;

public class GtoImportPreNotificationProcessor(
    IMongoContext mongoContext,
    ILogger<GtoImportPreNotificationProcessor> logger
) : IGtoImportPreNotificationProcessor
{
    public async Task ProcessAsync(
        ResourceEvent<ImportPreNotification> importPreNotificationEvent,
        CancellationToken cancellationToken
    )
    {
        var reference = importPreNotificationEvent.ResourceId;
        var importPreNotification = importPreNotificationEvent.Resource!;

        var importTransitResult = TransitValidation.IsTransit(importPreNotification);

        if (!importTransitResult.IsTransit)
        {
            logger.LogInformation("CHED {ChedId} is not a transit, skipping", reference);
            return;
        }

        var transitOverride = TransitOverride.IsTransitOverrideRequired(importPreNotification);

        var filter = Builders<ImportTransit>.Filter.Eq(x => x.Id, reference);
        var update = Builders<ImportTransit>
            .Update.SetOnInsert(x => x.Id, reference)
            .Set(x => x.TransitOverrideRequired, transitOverride.IsOverrideRequired)
            .Set(x => x.Mrn, importTransitResult.Mrn);
        var options = new FindOneAndUpdateOptions<ImportTransit> { IsUpsert = true };

        await mongoContext.ImportTransits.FindOneAndUpdate(filter, update, options, cancellationToken);

        logger.LogInformation("Inserted or updated ImportTransit {Id}", reference);
    }
}
