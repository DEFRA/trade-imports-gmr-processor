namespace GmrProcessor.Services;

public interface IGvmsApiClientService
{
    Task PlaceOrReleaseHold(string gmrId, bool holdStatus, CancellationToken cancellationToken);
}
