using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace RunCoach.Api.Infrastructure;

/// <summary>
/// Bridges the ASP.NET Core 10 antiforgery middleware's feature-flag output
/// into an RFC 7807 ProblemDetails 400 response for MVC controllers.
/// <c>UseAntiforgery</c> sets <see cref="IAntiforgeryValidationFeature"/>
/// on validation failure but only Minimal API form-binding short-circuits
/// it to a 400 automatically; MVC controllers do not. Pairing every
/// <c>[RequireAntiforgeryToken]</c> endpoint with this bridge keeps the
/// 4xx contract consistent with the rest of the auth stack (DEC-055).
/// </summary>
public static class AntiforgeryBridgeMiddleware
{
    private const string AntiforgeryProblemType =
        "https://runcoach.app/problems/antiforgery-validation-failed";

    /// <summary>
    /// Installs the antiforgery → ProblemDetails bridge. Must run after
    /// <see cref="AntiforgeryApplicationBuilderExtensions.UseAntiforgery(IApplicationBuilder)"/>
    /// so the feature is available when the bridge inspects it.
    /// </summary>
    public static IApplicationBuilder UseAntiforgeryProblemDetailsBridge(this IApplicationBuilder app)
    {
        return app.Use(async (ctx, next) =>
        {
            if (ctx.Features.Get<IAntiforgeryValidationFeature>() is { IsValid: false })
            {
                await WriteProblemDetailsAsync(ctx);
                return;
            }

            await next(ctx);
        });
    }

    private static async Task WriteProblemDetailsAsync(HttpContext ctx)
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        var problemDetailsService = ctx.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = ctx,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Antiforgery validation failed.",
                Type = AntiforgeryProblemType,
                Detail = "The antiforgery cookie or header was missing or did not match. Call GET /api/v1/auth/xsrf to seed fresh tokens and retry.",
            },
        });
    }
}
