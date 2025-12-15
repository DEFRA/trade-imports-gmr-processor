namespace GmrProcessor.Data.Eta;

public interface IEtaGmrCollection : IMongoCollectionSet<EtaGmr>
{
    Task<EtaGmr?> UpdateOrInsert(EtaGmr gmr, CancellationToken cancellationToken);
}
