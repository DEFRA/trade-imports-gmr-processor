using Defra.TradeImportsGmrFinder.GvmsClient.Client;
using GmrProcessor.Data;

namespace GmrProcessor.Services;

public class GvmsApiClientService(
    ILogger<GvmsApiClientService> logger,
    IGvmsApiClient gvmsApiClient,
    IMongoContext mongo
) : IGvmsApiClientService
{
    public async Task PlaceOrReleaseHold(string gmrId, bool holdStatus, CancellationToken cancellationToken)
    {
        logger.LogInformation("{Action} hold on {GmrId}", holdStatus ? "Placing" : "Releasing", gmrId);

        await gvmsApiClient.HoldGmr(gmrId, holdStatus, cancellationToken);

        await mongo.GtoGmr.UpdateHoldStatus(gmrId, holdStatus, cancellationToken);
    }
}
