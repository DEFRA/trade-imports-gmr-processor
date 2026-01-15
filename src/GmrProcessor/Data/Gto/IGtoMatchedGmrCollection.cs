using Defra.TradeImportsGmrFinder.Domain.Events;

namespace GmrProcessor.Data.Gto;

public interface IGtoMatchedGmrCollection
{
    Task UpsertMatchedItem(MatchedGmr matchedGmr, CancellationToken cancellationToken);
    Task<List<string>> GetRelatedMrns(string gmrId, CancellationToken cancellationToken);
    Task<MatchedGmrItem?> GetByMrn(string mrn, CancellationToken cancellationToken);
}
