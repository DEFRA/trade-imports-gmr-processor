using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using GmrProcessor.Data;
using GmrProcessor.Services;
using MongoDB.Driver;

namespace GmrProcessor.Processors.Gto;

public class GtoImportPreNotificationProcessor(
    IMongoContext mongoContext,
    ILogger<GtoImportPreNotificationProcessor> logger,
    IGvmsApiClientService gvmsApiClientService,
    IGtoMatchedGmrRepository matchedGmrRepository
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
        var options = new FindOneAndUpdateOptions<ImportTransit>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.Before,
        };

        var originalTransitOverrideRecord = await mongoContext.ImportTransits.FindOneAndUpdate(
            filter,
            update,
            options,
            cancellationToken
        );

        logger.LogInformation("Inserted or updated ImportTransit {Id}", reference);

        if (
            originalTransitOverrideRecord is not null
            && transitOverride.IsOverrideRequired != originalTransitOverrideRecord.TransitOverrideRequired
        )
        {
            await PlaceOrReleaseHold(importTransitResult.Mrn!, transitOverride.IsOverrideRequired, cancellationToken);
        }
    }

    private async Task PlaceOrReleaseHold(string mrn, bool holdStatus, CancellationToken cancellationToken)
    {
        var matchedGmr = await matchedGmrRepository.GetByMrn(mrn, cancellationToken);
        if (matchedGmr is null)
        {
            logger.LogInformation(
                "Tried to {HoldStatus} on MRN {Mrn} but no MatchedGmr exists",
                holdStatus ? "place hold" : "release",
                mrn
            );
            return;
        }

        await gvmsApiClientService.PlaceOrReleaseHold(matchedGmr.GmrId, holdStatus, cancellationToken);
    }
}
