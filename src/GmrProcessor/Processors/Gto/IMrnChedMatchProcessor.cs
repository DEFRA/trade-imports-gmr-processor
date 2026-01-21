using Defra.TradeImportsDataApi.Domain.CustomsDeclaration;
using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;

namespace GmrProcessor.Processors.Gto;

public interface IMrnChedMatchProcessor
{
    Task<MrnChedMatchProcessorResult> ProcessCustomsDeclaration(
        ResourceEvent<CustomsDeclaration> customsDeclarationEvent,
        CancellationToken cancellationToken
    );
}
