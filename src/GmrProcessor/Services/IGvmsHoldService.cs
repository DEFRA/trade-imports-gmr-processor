namespace GmrProcessor.Services;

public interface IGvmsHoldService
{
    Task<GvmsHoldResult> PlaceOrReleaseHold(string gmrId, CancellationToken cancellationToken);
}
