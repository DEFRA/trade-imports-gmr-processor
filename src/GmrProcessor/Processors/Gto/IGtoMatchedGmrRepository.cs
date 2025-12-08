using Defra.TradeImportsGmrFinder.Domain.Events;
using GmrProcessor.Data;
using GmrProcessor.Data.Gto;

namespace GmrProcessor.Processors.Gto;

public interface IGtoMatchedGmrRepository
{
    Task<GtoGmr> UpsertGmr(GtoGmr gmr, CancellationToken cancellationToken);
    Task UpsertMatchedItem(MatchedGmr matchedGmr, CancellationToken cancellationToken);
    Task<List<string>> GetRelatedMrns(string gmrId, CancellationToken cancellationToken);
    Task<MatchedGmrItem?> GetByMrn(string mrn, CancellationToken cancellationToken);
}
