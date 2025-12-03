namespace GmrProcessor.Data.Gto;

public interface IGtoGmrCollection : IMongoCollectionSet<GtoGmr>
{
    Task<GtoGmr> UpdateOrInsert(GtoGmr gmr, CancellationToken cancellationToken);
    Task UpdateHoldStatus(string gmrId, bool holdStatus, CancellationToken cancellationToken);
}
