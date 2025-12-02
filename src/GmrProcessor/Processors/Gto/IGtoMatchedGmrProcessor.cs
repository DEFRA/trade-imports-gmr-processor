using Defra.TradeImportsGmrFinder.Domain.Events;

namespace GmrProcessor.Processors.Gto;

public interface IGtoMatchedGmrProcessor
{
    Task Process(MatchedGmr matchedGmr, CancellationToken cancellationToken);
}
