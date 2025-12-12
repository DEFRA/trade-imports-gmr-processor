using GmrProcessor.Data;

namespace GmrProcessor.Processors.Gto;

public class GtoImportTransitRepository(IMongoContext mongo) : IGtoImportTransitRepository
{
    public Task<ImportTransit?> GetByMrn(string mrn, CancellationToken cancellationToken) =>
        mongo.ImportTransits.FindOne(it => it.Mrn == mrn, cancellationToken);

    public Task<List<ImportTransit>> GetByMrns(List<string> mrns, CancellationToken cancellationToken)
    {
        return mongo.ImportTransits.FindMany<object>(f => f.Mrn != null && mrns.Contains(f.Mrn), cancellationToken);
    }
}
