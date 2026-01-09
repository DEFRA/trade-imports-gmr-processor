using System.Text;
using GmrProcessor.Config;
using Microsoft.Extensions.Options;

namespace GmrProcessor.Security;

public sealed class BasicAuthEndpointFilter(
    IOptions<FeatureOptions> featureOptions,
    ILogger<BasicAuthEndpointFilter> logger
) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var options = featureOptions.Value;
        if (
            string.IsNullOrWhiteSpace(options.DevEndpointUsername)
            || string.IsNullOrWhiteSpace(options.DevEndpointPassword)
        )
        {
            logger.LogError("Dev endpoint auth not configured");
            return Results.Problem(
                "Dev endpoint auth not configured.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }

        var authorization = context.HttpContext.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization))
            authorization = null;

        if (TryValidate(authorization, options.DevEndpointUsername, options.DevEndpointPassword))
            return await next(context);

        logger.LogWarning("Unauthorized dev endpoint request.");
        return Results.Unauthorized();
    }

    private static bool TryValidate(string? authorization, string expectedUsername, string expectedPassword)
    {
        if (string.IsNullOrWhiteSpace(authorization))
            return false;

        if (!authorization.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return false;

        var encoded = authorization["Basic ".Length..].Trim();
        if (encoded.Length == 0)
            return false;

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        catch (FormatException)
        {
            return false;
        }

        var separatorIndex = decoded.IndexOf(':');
        if (separatorIndex < 0)
            return false;

        var providedUsername = decoded[..separatorIndex];
        var providedPassword = decoded[(separatorIndex + 1)..];
        if (
            !string.Equals(providedUsername, expectedUsername, StringComparison.Ordinal)
            || !string.Equals(providedPassword, expectedPassword, StringComparison.Ordinal)
        )
        {
            return false;
        }

        return true;
    }
}
