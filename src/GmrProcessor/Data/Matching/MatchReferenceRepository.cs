using GmrProcessor.Data.Common;
using GmrProcessor.Data.Gto;

namespace GmrProcessor.Data.Matching;

public class MatchReferenceRepository(
    IMongoContext mongoContext,
    IGtoImportTransitCollection gtoImportTransitCollection
) : IMatchReferenceRepository
{
    public async Task<List<string>> GetChedsByMrn(string mrn, CancellationToken cancellationToken)
    {
        var mrnChedMatchTask = mongoContext.MrnChedMatches.FindOne(m => m.Id == mrn, cancellationToken);

        var importTransitsTask = gtoImportTransitCollection.GetByMrns([mrn], cancellationToken);

        await Task.WhenAll(mrnChedMatchTask, importTransitsTask);

        return (mrnChedMatchTask.Result?.ChedReferences ?? Enumerable.Empty<string>())
            .Concat(importTransitsTask.Result.Select(it => it.Id))
            .Distinct()
            .ToList();
    }
}
