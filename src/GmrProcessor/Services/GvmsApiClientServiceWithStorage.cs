using System.Text.Json;
using GmrProcessor.Data;
using MongoDB.Driver;

namespace GmrProcessor.Services;

public class GvmsApiClientServiceWithStorage(
    IGvmsApiClientService gvmsApiClient,
    IMongoContext mongoContext,
    ILogger<GvmsApiClientServiceWithStorage> logger
) : IGvmsApiClientService
{
    public async Task PlaceOrReleaseHold(string gmrId, bool holdStatus, CancellationToken cancellationToken)
    {
        try
        {
            await gvmsApiClient.PlaceOrReleaseHold(gmrId, holdStatus, cancellationToken);
        }
        finally
        {
            await StoreMessage(gmrId, holdStatus, cancellationToken);
        }
    }

    private async Task StoreMessage(string gmrId, bool holdStatus, CancellationToken cancellationToken)
    {
        try
        {
            var messageAudit = new MessageAudit
            {
                Direction = MessageDirection.Outbound,
                IntegrationType = IntegrationType.GvmsApi,
                Target = "GVMS Hold API",
                MessageBody = JsonSerializer.Serialize(new { gmrId, holdStatus }),
                Timestamp = DateTime.UtcNow,
                MessageType = "GvmsHoldRequest",
            };

            WriteModel<MessageAudit> bulkOp = new InsertOneModel<MessageAudit>(messageAudit);

            await mongoContext.MessageAudits.BulkWrite([bulkOp], cancellationToken);
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
