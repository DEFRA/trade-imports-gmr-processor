namespace GmrProcessor.Data.Gto;

public class GtoImportTransitCollection(IMongoContext mongo) : IGtoImportTransitCollection
{
    public Task<ImportTransit?> GetByMrn(string mrn, CancellationToken cancellationToken) =>
        mongo.ImportTransits.FindOne(it => it.Mrn == mrn, cancellationToken);

    public Task<List<ImportTransit>> GetByMrns(List<string> mrns, CancellationToken cancellationToken)
    {
        return mongo.ImportTransits.FindMany<object>(f => f.Mrn != null && mrns.Contains(f.Mrn), cancellationToken);
    }
}
