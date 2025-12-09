using Defra.TradeImportsDataApi.Api.Client;
using Defra.TradeImportsGmrFinder.Domain.Events;
using GmrProcessor.Data;
using GmrProcessor.Processors.Gto;
using MongoDB.Driver;

namespace GmrProcessor.Processors.ImportGmrMatching;

public class ImportMatchedGmrsProcessor(ITradeImportsDataApiClient api, IMongoContext mongoContext)
    : IImportMatchedGmrsProcessor
{
    public async Task<object> Process(MatchedGmr matchedGmr, CancellationToken cancellationToken)
    {
        var matchMrn = matchedGmr.Mrn!;

        var getImportsTask = GetImportsByMrn(matchMrn, cancellationToken);
        var getTransitsTask = GetTransitByMrn(matchMrn, cancellationToken);

        await Task.WhenAll(getImportsTask, getTransitsTask);

        List<string> relatedImports = [.. getImportsTask.Result, .. getTransitsTask.Result];

        var previouslyMatched = await mongoContext.MatchedImportNotifications.FindMany<MatchedImportNotification>(
            match => relatedImports.Contains(match.Id),
            cancellationToken
        );

        var unmatched = relatedImports.Except(previouslyMatched.Select(m => m.Id));

        var bulkOperations = unmatched
            .Select(um =>
            {
                var notification = new MatchedImportNotification
                {
                    Id = um,
                    Mrn = matchMrn,
                    CreatedDateTime = DateTime.UtcNow,
                };
                return new ReplaceOneModel<MatchedImportNotification>(
                    Builders<MatchedImportNotification>.Filter.Eq(n => n.Id, um),
                    notification
                )
                {
                    IsUpsert = true,
                };
            })
            .ToList<WriteModel<MatchedImportNotification>>();

        if (bulkOperations.Count > 0)
        {
            await mongoContext.MatchedImportNotifications.BulkWrite(bulkOperations, cancellationToken);
        }

        return new object();
    }

    private async Task<string[]> GetTransitByMrn(string mrn, CancellationToken cancellationToken)
    {
        var transits = await mongoContext.ImportTransits.FindMany<ImportTransit>(
            it => it.Mrn == mrn,
            cancellationToken
        );

        return transits.Select(it => it.Id).ToArray();
    }

    private async Task<string[]> GetImportsByMrn(string matchMrn, CancellationToken cancellationToken)
    {
        var importPreNotifications = await api.GetImportPreNotificationsByMrn(matchMrn, cancellationToken);

        return importPreNotifications
            .ImportPreNotifications.Select(i => i.ImportPreNotification.ReferenceNumber!)
            .ToArray();
    }
}
