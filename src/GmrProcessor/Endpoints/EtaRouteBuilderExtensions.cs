using GmrProcessor.Data.Eta;
using GmrProcessor.Security;
using Microsoft.AspNetCore.Mvc;

namespace GmrProcessor.Endpoints;

public static class EtaRouteBuilderExtensions
{
    public static void MapEtaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/eta/mrn/{mrn}", GetEtaByMrn).AddEndpointFilter<BasicAuthEndpointFilter>();
    }

    [HttpGet]
    public static async Task<IResult> GetEtaByMrn(
        [FromServices] IEtaGmrCollection etaGmrCollection,
        [FromRoute] string mrn,
        CancellationToken cancellationToken
    )
    {
        var result = await etaGmrCollection.FindOne(
            f =>
                f.Gmr.Declarations != null
                && f.Gmr.Declarations.Customs != null
                && f.Gmr.Declarations.Customs.Any(c => c.Id == mrn),
            cancellationToken
        );

        return result is not null ? Results.Ok(result) : Results.NotFound();
    }
}
