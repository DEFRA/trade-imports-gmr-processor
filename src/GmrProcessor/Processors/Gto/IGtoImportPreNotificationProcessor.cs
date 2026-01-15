using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;

namespace GmrProcessor.Processors.Gto;

public interface IGtoImportPreNotificationProcessor
{
    Task<GtoImportNotificationProcessorResult> Process(
        ResourceEvent<ImportPreNotification> importPreNotificationEvent,
        CancellationToken cancellationToken
    );
}
