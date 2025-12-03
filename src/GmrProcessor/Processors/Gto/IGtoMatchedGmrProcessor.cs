using Defra.TradeImportsGmrFinder.Domain.Events;

namespace GmrProcessor.Processors.Gto;

public interface IGtoMatchedGmrProcessor
{
    Task<GtoMatchedGmrProcessResult> Process(MatchedGmr matchedGmr, CancellationToken cancellationToken);
}
