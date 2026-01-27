using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using GmrProcessor.Data;
using GmrProcessor.Data.Common;
using GmrProcessor.Data.Gto;
using GmrProcessor.Logging;
using GmrProcessor.Services;
using GmrProcessor.Utils;
using MongoDB.Driver;

namespace GmrProcessor.Processors.Gto;

public class GtoImportPreNotificationProcessor(
    IMongoContext mongoContext,
    ILogger<GtoImportPreNotificationProcessor> logger,
    IGtoMatchedGmrCollection matchedGmrCollection,
    IGvmsHoldService gvmsHoldService
) : IGtoImportPreNotificationProcessor
{
    private readonly ILogger<GtoImportPreNotificationProcessor> _logger =
        new PrefixedLogger<GtoImportPreNotificationProcessor>(logger, "GTO");

    public async Task<GtoImportNotificationProcessorResult> Process(
        ResourceEvent<ImportPreNotification> importPreNotificationEvent,
        CancellationToken cancellationToken
    )
    {
        var reference = importPreNotificationEvent.ResourceId;
        var importPreNotification = importPreNotificationEvent.Resource!;

        var importTransitResult = TransitValidation.IsTransit(importPreNotification);

        if (!importTransitResult.IsTransit)
        {
            _logger.LogInformation("CHED {ChedId} is not a transit, skipping", reference);
            return GtoImportNotificationProcessorResult.SkippedNotATransit;
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

        await mongoContext.ImportTransits.FindOneAndUpdate(filter, update, options, cancellationToken);

        _logger.LogInformation("Inserted or updated ImportTransit {Id}", reference);

        var mrn = importTransitResult.Mrn!;

        var matchedGmr = await matchedGmrCollection.GetByMrn(mrn, cancellationToken);
        if (matchedGmr is null)
        {
            _logger.LogInformation("Tried to place or release hold on MRN {Mrn} but no MatchedGmr exists", mrn);
            return GtoImportNotificationProcessorResult.NoMatchedGmrExists;
        }

        var result = await gvmsHoldService.PlaceOrReleaseHold(matchedGmr.GmrId, cancellationToken);
        return result switch
        {
            GvmsHoldResult.HoldPlaced => GtoImportNotificationProcessorResult.HoldPlaced,
            GvmsHoldResult.HoldReleased => GtoImportNotificationProcessorResult.HoldReleased,
            GvmsHoldResult.NoHoldChange => GtoImportNotificationProcessorResult.NoHoldChange,
            _ => throw new InvalidOperationException($"Unexpected GvmsHoldResult value: {result}"),
        };
    }
}
