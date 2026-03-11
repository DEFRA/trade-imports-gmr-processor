using Defra.TradeImportsDataApi.Domain.Events;

namespace GmrProcessor.Processors.MrnChedMatch;

public interface IMrnChedMatchProcessor
{
    Task<MrnChedMatchProcessorResult> ProcessCustomsDeclaration(
        ResourceEvent<CustomsDeclarationEvent> customsDeclarationEvent,
        CancellationToken cancellationToken
    );
}
