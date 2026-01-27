using Defra.TradeImportsDataApi.Domain.CustomsDeclaration;
using Defra.TradeImportsDataApi.Domain.Events;
using GmrProcessor.Data;
using GmrProcessor.Logging;
using GmrProcessor.Utils;
using MongoDB.Driver;

namespace GmrProcessor.Processors.MrnChedMatch;

public class MrnChedMatchProcessor(IMongoContext mongoContext, ILogger<MrnChedMatchProcessor> logger)
    : IMrnChedMatchProcessor
{
    private readonly ILogger<MrnChedMatchProcessor> _logger = new PrefixedLogger<MrnChedMatchProcessor>(
        logger,
        "MrnChedMatch"
    );

    public async Task<MrnChedMatchProcessorResult> ProcessCustomsDeclaration(
        ResourceEvent<CustomsDeclaration> customsDeclarationEvent,
        CancellationToken cancellationToken
    )
    {
        var mrn = customsDeclarationEvent.ResourceId.ToUpperInvariant();
        var customsDeclaration = customsDeclarationEvent.Resource!;

        if (!MrnRegex.Value().IsMatch(mrn))
        {
            _logger.LogWarning("CustomsDeclaration has invalid MRN format: {Mrn}, skipping", mrn);
            return MrnChedMatchProcessorResult.SkippedInvalidMrn;
        }

        var chedReferences =
            customsDeclaration
                .ClearanceDecision?.Results?.Where(r => r.ImportPreNotification != null)
                .Select(r => r.ImportPreNotification!)
                .Distinct()
                .ToList() ?? [];

        if (chedReferences.Count == 0)
        {
            _logger.LogInformation("CustomsDeclaration {Mrn} has no CHED references, skipping", mrn);
            return MrnChedMatchProcessorResult.SkippedNoChedReferences;
        }

        var existingMatch = await mongoContext.MrnChedMatches.FindOne(m => m.Id == mrn, cancellationToken);

        var match = new Data.Matching.MrnChedMatch
        {
            Id = mrn,
            ChedReferences = chedReferences,
            CreatedDateTime = existingMatch?.CreatedDateTime ?? DateTime.UtcNow,
            UpdatedDateTime = DateTime.UtcNow,
        };

        await mongoContext.MrnChedMatches.ReplaceOne(
            m => m.Id == mrn,
            match,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken
        );

        _logger.LogInformation(
            "Processed CustomsDeclaration {Mrn} with {Count} CHED references: {ChedReferences}",
            mrn,
            chedReferences.Count,
            string.Join(", ", chedReferences)
        );

        return MrnChedMatchProcessorResult.MatchCreated;
    }
}
