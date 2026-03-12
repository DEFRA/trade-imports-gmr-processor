using GmrProcessor.Data.Common;

namespace GmrProcessor.Data.Gto;

public interface IGtoImportTransitCollection
{
    Task<List<ImportTransit>> GetAllByMrn(string mrn, CancellationToken cancellationToken);
    Task<List<ImportTransit>> GetByMrns(List<string> mrns, CancellationToken cancellationToken);
}
