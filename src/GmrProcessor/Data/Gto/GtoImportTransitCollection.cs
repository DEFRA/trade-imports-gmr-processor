using GmrProcessor.Data.Common;

namespace GmrProcessor.Data.Gto;

public class GtoImportTransitCollection(IMongoContext mongo) : IGtoImportTransitCollection
{
    public Task<List<ImportTransit>> GetAllByMrn(string mrn, CancellationToken cancellationToken) =>
        mongo.ImportTransits.FindMany<ImportTransit>(it => it.Mrn == mrn, cancellationToken);

    public Task<List<ImportTransit>> GetByMrns(List<string> mrns, CancellationToken cancellationToken)
    {
        return mongo.ImportTransits.FindMany<ImportTransit>(
            f => f.Mrn != null && mrns.Contains(f.Mrn),
            cancellationToken
        );
    }
}
