using Defra.TradeImportsDataApi.Domain.CustomsDeclaration;
using Defra.TradeImportsDataApi.Domain.Events;

namespace GmrProcessor.Processors.MrnChedMatch;

public interface IMrnChedMatchProcessor
{
    Task<MrnChedMatchProcessorResult> ProcessCustomsDeclaration(
        ResourceEvent<CustomsDeclaration> customsDeclarationEvent,
        CancellationToken cancellationToken
    );
}
