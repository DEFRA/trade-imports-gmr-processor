using GmrProcessor.Data;

namespace GmrProcessor.Processors.Gto;

public interface IGtoImportTransitRepository
{
    Task<ImportTransit?> GetByMrn(string mrn, CancellationToken cancellationToken);
    Task<List<ImportTransit>> GetByMrns(List<string> mrns, CancellationToken cancellationToken);
}
