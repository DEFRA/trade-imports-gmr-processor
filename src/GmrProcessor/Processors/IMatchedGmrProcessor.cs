using Defra.TradeImportsGmrFinder.Domain.Events;

namespace GmrProcessor.Processors;

public interface IMatchedGmrProcessor
{
    Task Process(MatchedGmr matchedGmr, CancellationToken cancellationToken);
}