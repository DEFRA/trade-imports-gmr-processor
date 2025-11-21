using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;

namespace GmrProcessor.Processors;

public interface IGtoImportPreNotificationProcessor
{
    Task ProcessAsync(ResourceEvent<ImportPreNotification> importPreNotification, CancellationToken cancellationToken);
}
