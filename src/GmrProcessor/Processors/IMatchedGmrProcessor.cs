using Defra.TradeImportsGmrFinder.Domain.Events;

namespace GmrProcessor.Processors;

public interface IMatchedGmrProcessor<TResult>
{
    Task<TResult> Process(MatchedGmr matchedGmr, CancellationToken cancellationToken);
}
