using GmrProcessor.Data;
using GmrProcessor.Security;

namespace GmrProcessor.Endpoints;

public static class MessageEndpoints
{
    public static IEndpointRouteBuilder MapMessageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/messages", GetMessages).AddEndpointFilter<BasicAuthEndpointFilter>();

        return app;
    }

    private static async Task<IResult> GetMessages(
        string? messageType,
        IMessageAuditRepository repository,
        ILogger<Program> logger,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(messageType))
        {
            return Results.BadRequest(new { error = "messageType query parameter is required" });
        }

        try
        {
            var fromTimestamp = DateTime.UtcNow.AddMinutes(-15);
            var messages = await repository.GetByMessageTypeAsync(messageType, fromTimestamp, cancellationToken);

            return Results.Ok(messages);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to query messages for messageType: {MessageType}", messageType);
            return Results.Problem(
                "An error occurred while querying messages",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }
}
