using System.Net;
using System.Net.Http.Headers;
using CCOS.Shared;

namespace CCOS.Web.Services;

/// <summary>
/// Calls the sign-in endpoints in CCOS.Api (CAR-67): create an account, log in, refresh the
/// sign-in token, and "who am I?". The API address comes from Api:BaseUrl in appsettings.json.
/// Unlike the other API clients, this one does not add the signed-in user's token on its own:
/// it is used while signing in, before there is a token, and while the cookie is being checked.
/// </summary>
public sealed class AccountApiClient(HttpClient http)
{
    /// <summary>
    /// Sends a new account to POST /api/account/register. The caller checks the returned response
    /// (success or validation errors) and disposes it.
    /// </summary>
    public Task<HttpResponseMessage> RegisterAsync(RegisterAccountRequest request, CancellationToken cancellationToken = default)
    {
        return http.PostAsJsonAsync("api/account/register", request, cancellationToken);
    }

    /// <summary>
    /// Signs in through POST /api/auth/login. Returns the tokens, or null when the email or
    /// password is wrong (the API answers 401 for both, on purpose).
    /// </summary>
    public async Task<LoginTokens?> LoginAsync(string email, string accountSecret, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(
            "api/auth/login",
            new { email, password = accountSecret },
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LoginTokens>(cancellationToken);
    }

    /// <summary>
    /// Trades a refresh token for new tokens through POST /api/auth/refresh. Null when the
    /// refresh token is expired or no longer valid, so the person has to sign in again.
    /// </summary>
    public async Task<LoginTokens?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/auth/refresh", new { refreshToken }, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LoginTokens>(cancellationToken);
    }

    /// <summary>
    /// Who the token belongs to, from GET /api/me. Null when the token isn't accepted.
    /// The token is passed in because this runs right after login, before the cookie exists.
    /// </summary>
    public async Task<CurrentAccountDto?> GetMeAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CurrentAccountDto>(cancellationToken);
    }
}

/// <summary>
/// The tokens Identity's login and refresh endpoints return. The access token goes on every
/// API call; the refresh token gets a new access token when it runs out (ExpiresIn, in seconds).
/// </summary>
public sealed record LoginTokens(string TokenType, string AccessToken, long ExpiresIn, string RefreshToken);
