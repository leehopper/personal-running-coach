using System.Net;

namespace RunCoach.Api.Tests.Infrastructure;

/// <summary>
/// <see cref="DelegatingHandler"/> that threads a <see cref="CookieContainer"/>
/// through the in-memory <c>HttpClient</c> produced by
/// <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}.CreateDefaultClient(System.Net.Http.DelegatingHandler[])"/>.
/// The default TestServer client does not persist <c>Set-Cookie</c> state
/// across requests, so the auth flow (antiforgery → register → login → me →
/// logout) cannot be exercised without this seam. Cookies set in a response
/// are parsed back into the supplied container, and the container's
/// matching header is attached on every outgoing request.
/// </summary>
public sealed class CookieContainerHandler(CookieContainer container) : DelegatingHandler
{
    /// <summary>Gets cookies captured across requests, shared with the caller.</summary>
    public CookieContainer Container { get; } = container;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is not null)
        {
            var cookieHeader = Container.GetCookieHeader(request.RequestUri);
            if (!string.IsNullOrEmpty(cookieHeader))
            {
                request.Headers.Remove("Cookie");
                request.Headers.Add("Cookie", cookieHeader);
            }
        }

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (request.RequestUri is not null &&
            response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            foreach (var setCookie in setCookies)
            {
                // CookieContainer.SetCookies parses a single Set-Cookie value
                // per call — each TryGetValues entry is one full cookie string.
                Container.SetCookies(request.RequestUri, setCookie);
            }
        }

        return response;
    }
}
