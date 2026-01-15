namespace GmrProcessor.Data.Gto;

public interface IGtoImportTransitCollection
{
    Task<ImportTransit?> GetByMrn(string mrn, CancellationToken cancellationToken);
    Task<List<ImportTransit>> GetByMrns(List<string> mrns, CancellationToken cancellationToken);
}
