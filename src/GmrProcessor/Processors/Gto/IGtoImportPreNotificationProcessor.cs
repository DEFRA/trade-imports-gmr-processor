using Defra.TradeImportsDataApi.Domain.Events;

namespace GmrProcessor.Processors.Gto;

public interface IGtoImportPreNotificationProcessor
{
    Task<GtoImportNotificationProcessorResult> Process(
        ResourceEvent<ImportPreNotificationEvent> importPreNotificationEvent,
        CancellationToken cancellationToken
    );
}
