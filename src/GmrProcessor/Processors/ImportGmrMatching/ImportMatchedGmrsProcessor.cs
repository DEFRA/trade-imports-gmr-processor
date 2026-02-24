using Defra.TradeImportsGmrFinder.Domain.Events;
using GmrProcessor.Config;
using GmrProcessor.Data;
using GmrProcessor.Data.Common;
using GmrProcessor.Data.ImportGmrMatching;
using GmrProcessor.Logging;
using GmrProcessor.Processors.Gto;
using GmrProcessor.Services;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GmrProcessor.Processors.ImportGmrMatching;

public class ImportMatchedGmrsProcessor(
    IMongoContext mongoContext,
    IMatchReferenceRepository matchReferenceRepository,
    ITradeImportsServiceBus tradeImportsServiceBus,
    IOptions<TradeImportsServiceBusOptions> serviceBusOptions,
    ILogger<ImportMatchedGmrsProcessor> logger
) : IImportMatchedGmrsProcessor
{
    private readonly ServiceBusQueue _importMatchQueueOptions = serviceBusOptions.Value.ImportMatchResult;
    private readonly ILogger<ImportMatchedGmrsProcessor> _logger = new PrefixedLogger<ImportMatchedGmrsProcessor>(
        logger,
        "ImportGmrMatching"
    );

    public async Task<ImportMatchedGmrsProcessorResult> Process(
        MatchedGmr matchedGmr,
        CancellationToken cancellationToken
    )
    {
        var matchMrn = matchedGmr.Mrn!;
        var relatedImports = await matchReferenceRepository.GetChedsByMrn(matchMrn, cancellationToken);

        if (relatedImports.Count == 0)
        {
            _logger.LogInformation("Skipping {Mrn} because no related imports have been found", matchedGmr.Mrn);
            return ImportMatchedGmrsProcessorResult.NoRelatedImportsFound;
        }

        var previouslyMatched = await mongoContext.MatchedImportNotifications.FindMany<MatchedImportNotification>(
            match => relatedImports.Contains(match.Id),
            cancellationToken
        );

        var unmatched = relatedImports.Except(previouslyMatched.Select(m => m.Id));

        var enumerable = unmatched as string[] ?? unmatched.ToArray();

        var bulkOperations = enumerable
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

        if (bulkOperations.Count == 0)
        {
            _logger.LogInformation("Received matched GMR {GmrId}, but no updates to send", matchedGmr.Gmr.GmrId);
            return ImportMatchedGmrsProcessorResult.NoUpdatesFound;
        }

        _logger.LogInformation(
            "Received matched GMR {GmrId}, updating Ipaffs with Mrns: {Mrns}",
            matchedGmr.Gmr.GmrId,
            string.Join(",", enumerable.ToList())
        );

        var messages = enumerable.Select(um => new ImportMatchMessage { ReferenceNumber = um, Match = true });
        await tradeImportsServiceBus.SendMessagesAsync(messages, _importMatchQueueOptions.QueueName, cancellationToken);
        await mongoContext.MatchedImportNotifications.BulkWrite(bulkOperations, cancellationToken);

        return ImportMatchedGmrsProcessorResult.UpdatedIpaffs;
    }
}
