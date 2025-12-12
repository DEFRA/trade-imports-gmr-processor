namespace GmrProcessor.Data.Eta;

public interface IEtaGmrCollection
{
    Task<EtaGmr?> UpdateOrInsert(EtaGmr gmr, CancellationToken cancellationToken);
}
