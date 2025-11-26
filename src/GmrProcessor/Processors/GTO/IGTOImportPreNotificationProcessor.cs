using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;

namespace GmrProcessor.Processors.GTO;

public interface IGtoImportPreNotificationProcessor
{
    Task ProcessAsync(
        ResourceEvent<ImportPreNotification> importPreNotificationEvent,
        CancellationToken cancellationToken
    );
}
