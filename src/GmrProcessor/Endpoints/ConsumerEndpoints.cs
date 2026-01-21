using System.Text.Json;
using Defra.TradeImportsDataApi.Domain.CustomsDeclaration;
using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using GmrProcessor.Processors.Gto;
using GmrProcessor.Security;
using Microsoft.AspNetCore.Mvc;

namespace GmrProcessor.Endpoints;

public static class ConsumerEndpoints
{
    public static IEndpointRouteBuilder MapConsumerEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/consumers/data-events-queue", Post).AddEndpointFilter<BasicAuthEndpointFilter>();

        return app;
    }

    private static async Task<IResult> Post(
        ILoggerFactory loggerFactory,
        IMrnChedMatchProcessor mrnChedMatchProcessor,
        IGtoImportPreNotificationProcessor importPreNotificationProcessor,
        [FromHeader(Name = "ResourceType")] string? resourceType,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken
    )
    {
        var logger = loggerFactory.CreateLogger("ConsumerEndpoints");

        if (string.IsNullOrWhiteSpace(resourceType))
        {
            return Results.BadRequest(new { error = "ResourceType header is required" });
        }

        try
        {
            switch (resourceType)
            {
                case ResourceEventResourceTypes.CustomsDeclaration:
                    var customsDeclaration = DeserializeEvent<CustomsDeclaration>(body, logger);
                    if (customsDeclaration == null)
                    {
                        return Results.BadRequest(new { error = "Failed to deserialize CustomsDeclaration payload" });
                    }
                    await mrnChedMatchProcessor.ProcessCustomsDeclaration(customsDeclaration, cancellationToken);
                    return Results.Accepted();

                case ResourceEventResourceTypes.ImportPreNotification:
                    var importPreNotification = DeserializeEvent<ImportPreNotification>(body, logger);
                    if (importPreNotification == null)
                    {
                        return Results.BadRequest(
                            new { error = "Failed to deserialize ImportPreNotification payload" }
                        );
                    }
                    await importPreNotificationProcessor.Process(importPreNotification, cancellationToken);
                    return Results.Accepted();

                default:
                    logger.LogWarning("Unhandled ResourceType: {ResourceType}", resourceType);
                    return Results.BadRequest(new { error = "Unsupported resourceType" });
            }
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize JSON payload for ResourceType: {ResourceType}", resourceType);
            return Results.BadRequest(new { error = "Invalid JSON payload" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing event for ResourceType: {ResourceType}", resourceType);
            return Results.Problem(
                "An error occurred while processing the event",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    private static ResourceEvent<T>? DeserializeEvent<T>(JsonElement body, ILogger logger)
    {
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        try
        {
            return body.Deserialize<ResourceEvent<T>>(serializerOptions);
        }
        catch (JsonException ex)
        {
            logger.LogDebug(
                ex,
                "Failed to deserialize JSON to ResourceEvent<{Type}>: {Json}",
                typeof(T).FullName,
                body.GetRawText()
            );
            throw new JsonException($"Failed to deserialize JSON to ResourceEvent<{typeof(T).FullName}>.", ex);
        }
    }
}
