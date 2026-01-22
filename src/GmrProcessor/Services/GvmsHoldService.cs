using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Defra.TradeImportsGmrFinder.GvmsClient.Client;
using GmrProcessor.Config;
using GmrProcessor.Data;
using GmrProcessor.Data.Auditing;
using GmrProcessor.Data.Gto;
using GmrProcessor.Metrics;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GmrProcessor.Services;

[SuppressMessage("Minor Code Smell", "S107:Methods should not have too many parameters")]
public class GvmsHoldService(
    ILogger<GvmsHoldService> logger,
    IOptions<FeatureOptions> features,
    IMongoContext mongo,
    IGvmsApiClient gvmsApiClient,
    IGtoMatchedGmrCollection matchedGmrCollection,
    IGtoImportTransitCollection gtoImportTransitCollection,
    IGtoGmrCollection gtoGmrCollection,
    IGvmsApiMetrics metrics
) : IGvmsHoldService
{
    private readonly FeatureOptions _features = features.Value;

    public async Task<GvmsHoldResult> PlaceOrReleaseHold(string gmrId, CancellationToken cancellationToken)
    {
        var gmr = await gtoGmrCollection.FindOne(g => g.Gmr.GmrId == gmrId, cancellationToken);
        if (gmr == null)
        {
            logger.LogWarning("Tried to place or release a hold on {GmrId} which was not found", gmrId);
            return GvmsHoldResult.NoHoldChange;
        }

        var relatedMrns = await matchedGmrCollection.GetRelatedMrns(gmrId, cancellationToken);
        var relatedImportTransits = await gtoImportTransitCollection.GetByMrns(relatedMrns, cancellationToken);

        if (relatedImportTransits.Count == 0)
        {
            logger.LogInformation("No related import transits were found for {GmrId}, taking no action", gmrId);
            return GvmsHoldResult.NoHoldChange;
        }

        var anyImportTransitsRequireHold = relatedImportTransits.Any(x => x.TransitOverrideRequired);

        if (gmr.HoldStatus == anyImportTransitsRequireHold)
        {
            logger.LogInformation(
                "Matched GMR {GmrId} is already {State}, no action was taken",
                gmrId,
                gmr.HoldStatus == true ? "on hold" : "released"
            );
            return GvmsHoldResult.NoHoldChange;
        }

        logger.LogInformation(
            "{Action} GVMS hold on {GmrId}",
            anyImportTransitsRequireHold ? "Placing" : "Releasing",
            gmrId
        );

        if (!_features.EnableGvmsApiClientHold)
        {
            logger.LogInformation("GVMS API client is disabled, skipping API call for {GmrId}", gmrId);
            return GvmsHoldResult.NoHoldChange;
        }

        try
        {
            await metrics.RecordRequest(
                "Hold",
                gvmsApiClient.HoldGmr(gmrId, anyImportTransitsRequireHold, cancellationToken)
            );
        }
        finally
        {
            await StoreGvmsHold(gmrId, relatedMrns, anyImportTransitsRequireHold, cancellationToken);
        }

        await mongo.GtoGmr.UpdateHoldStatus(gmrId, anyImportTransitsRequireHold, cancellationToken);

        return anyImportTransitsRequireHold ? GvmsHoldResult.HoldPlaced : GvmsHoldResult.HoldReleased;
    }

    private async Task StoreGvmsHold(
        string gmrId,
        List<string> mrns,
        bool holdStatus,
        CancellationToken cancellationToken
    )
    {
        if (!_features.EnableStoreOutboundMessages)
        {
            return;
        }

        try
        {
            var messageAudit = new MessageAudit
            {
                Direction = MessageDirection.Outbound,
                IntegrationType = IntegrationType.GvmsApi,
                Target = "GVMS Hold API",
                MessageBody = JsonSerializer.Serialize(
                    new GvmsHoldRecord
                    {
                        GmrId = gmrId,
                        Mrns = mrns,
                        Hold = holdStatus,
                    }
                ),
                Timestamp = DateTime.UtcNow,
                MessageType = "GvmsHoldRequest",
            };

            WriteModel<MessageAudit> bulkOp = new InsertOneModel<MessageAudit>(messageAudit);

            await mongo.MessageAudits.BulkWrite([bulkOp], cancellationToken);
            logger.LogInformation(
                "Stored GVMS hold request to MongoDB: GMR {GmrId}, Hold {HoldStatus}",
                gmrId,
                holdStatus
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to store GVMS hold request to MongoDB for GMR {GmrId}", gmrId);
        }
    }
}
