using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;

namespace CCOS.Web.Services;

/// <summary>
/// Adds the signed-in person's API token to every call the website makes to CCOS.Api
/// (products, events, venues), so the API knows who is asking (docs/CAR-52_Login_Approach.md, section 2).
/// The token lives inside the encrypted sign-in cookie on the server side; the browser never sees it.
/// Calls made by a visitor who isn't signed in go out without a token, as before.
/// </summary>
public sealed class ApiBearerTokenHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    /// <summary>Puts "Authorization: Bearer &lt;token&gt;" on the request when someone is signed in.</summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;

        // A request that already names a token (none of ours do today) keeps its own.
        if (request.Headers.Authorization is null && httpContext?.User.Identity?.IsAuthenticated == true)
        {
            var accessToken = await httpContext.GetTokenAsync(AccountSession.AccessTokenName);
            if (!string.IsNullOrEmpty(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
