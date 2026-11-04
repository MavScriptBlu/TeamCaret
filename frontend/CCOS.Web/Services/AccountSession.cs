using System.Globalization;
using System.Security.Claims;
using CCOS.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace CCOS.Web.Services;

/// <summary>
/// How a signed-in visit is kept (CAR-68): one encrypted, HttpOnly cookie that holds the person's
/// name and CustomerId plus their API tokens. It also notes whether they were a current member or
/// an officer when the token was last renewed (CAR-69), so the website can show the right links
/// and lock the officer pages. That's only for the website: the API still checks the database on
/// every call (CAR-67), so the cookie can't grant access to any data by itself.
/// </summary>
public static class AccountSession
{
    /// <summary>Names the tokens are stored under in the cookie.</summary>
    public const string AccessTokenName = "access_token";
    public const string RefreshTokenName = "refresh_token";
    public const string ExpiresAtName = "expires_at";

    /// <summary>The claim that holds the person's CustomerId.</summary>
    public const string CustomerIdClaim = "ccos:customer_id";

    /// <summary>Present ("true") when the person is a current member (CAR-69).</summary>
    public const string MemberClaim = "ccos:member";

    /// <summary>Present ("true") when the person is a current officer or the club's advisor (CAR-69).</summary>
    public const string OfficerClaim = "ccos:officer";

    /// <summary>Refresh the access token a minute before it runs out, so a call never goes out with a dead one.</summary>
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(1);

    /// <summary>The outcome of signing in with an email and password.</summary>
    public enum SignInOutcome
    {
        SignedIn,
        WrongCredentials,
        Failed,
    }

    /// <summary>
    /// Logs in through the API, looks up who the person is, then writes the sign-in cookie.
    /// WrongCredentials when the email or password is wrong; Failed when the API answered but
    /// the person couldn't be looked up. Network errors are left to the caller.
    /// </summary>
    public static async Task<SignInOutcome> SignInWithPasswordAsync(
        HttpContext httpContext,
        AccountApiClient accounts,
        string email,
        string accountSecret,
        CancellationToken cancellationToken)
    {
        var tokens = await accounts.LoginAsync(email.Trim(), accountSecret, cancellationToken);
        if (tokens is null)
        {
            return SignInOutcome.WrongCredentials;
        }

        var account = await accounts.GetMeAsync(tokens.AccessToken, cancellationToken);
        if (account is null)
        {
            return SignInOutcome.Failed;
        }

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            CreatePrincipal(account),
            CreateProperties(tokens, DateTimeOffset.UtcNow));

        return SignInOutcome.SignedIn;
    }

    /// <summary>
    /// The signed-in person as the website sees them: first name (shown in the nav), last name,
    /// email and CustomerId, plus member and officer flags (an advisor counts as an officer, same as the API).
    /// </summary>
    public static ClaimsPrincipal CreatePrincipal(CurrentAccountDto account)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, account.FirstName),
            new(ClaimTypes.GivenName, account.FirstName),
            new(ClaimTypes.Surname, account.LastName),
            new(ClaimTypes.Email, account.Email),
            new(CustomerIdClaim, account.CustomerId.ToString(CultureInfo.InvariantCulture)),
        };

        if (account.IsCurrentMember)
        {
            claims.Add(new Claim(MemberClaim, "true"));
        }

        if (account.IsCurrentOfficer || account.IsAdvisor)
        {
            claims.Add(new Claim(OfficerClaim, "true"));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    /// <summary>Cookie settings for a new sign-in: the tokens, and when the access token runs out.</summary>
    public static AuthenticationProperties CreateProperties(LoginTokens tokens, DateTimeOffset now)
    {
        var properties = new AuthenticationProperties { IsPersistent = false };
        StoreTokens(properties, tokens, now);
        return properties;
    }

    /// <summary>Saves (or replaces) the tokens and their expiry time in the cookie's properties.</summary>
    public static void StoreTokens(AuthenticationProperties properties, LoginTokens tokens, DateTimeOffset now)
    {
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = AccessTokenName, Value = tokens.AccessToken },
            new AuthenticationToken { Name = RefreshTokenName, Value = tokens.RefreshToken },
            new AuthenticationToken
            {
                Name = ExpiresAtName,
                Value = now.AddSeconds(tokens.ExpiresIn).ToString("o", CultureInfo.InvariantCulture),
            },
        ]);
    }

    /// <summary>True when the access token has run out, or will within a minute (or its expiry time is missing).</summary>
    public static bool NeedsRefresh(AuthenticationProperties properties, DateTimeOffset now)
    {
        var expiresAt = properties.GetTokenValue(ExpiresAtName);
        if (!DateTimeOffset.TryParse(expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expires))
        {
            return true;
        }

        return expires - RefreshMargin <= now;
    }

    /// <summary>
    /// Runs on every request that carries the sign-in cookie. When the API token is about to run
    /// out, trades the refresh token for a new one and updates the cookie. If that fails, the
    /// person is signed out and will be asked to log in again.
    /// </summary>
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var now = DateTimeOffset.UtcNow;
        if (!NeedsRefresh(context.Properties, now))
        {
            return;
        }

        var refreshToken = context.Properties.GetTokenValue(RefreshTokenName);
        var accounts = context.HttpContext.RequestServices.GetRequiredService<AccountApiClient>();
        LoginTokens? refreshed = null;
        CurrentAccountDto? account = null;

        if (!string.IsNullOrEmpty(refreshToken))
        {
            try
            {
                refreshed = await accounts.RefreshAsync(refreshToken, context.HttpContext.RequestAborted);

                // Read the person again too, so a renewal, an expired membership or a new officer
                // title shows up in the website's links and locked pages (CAR-69).
                if (refreshed is not null)
                {
                    account = await accounts.GetMeAsync(refreshed.AccessToken, context.HttpContext.RequestAborted);
                }
            }
            // The API is down or slow: log it, then treat it like a failed refresh and sign the person out.
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger(typeof(AccountSession))
                    .LogWarning(ex, "Could not refresh the sign-in token; signing the person out.");
            }
        }

        if (refreshed is null || account is null)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        StoreTokens(context.Properties, refreshed, now);
        context.ReplacePrincipal(CreatePrincipal(account));
        context.ShouldRenew = true;
    }

    /// <summary>
    /// The page to go to after signing in: the one the person came from when it's a page on this
    /// site, otherwise the merch page. Stops links that would send someone to another website.
    /// </summary>
    public static string SafeReturnUrl(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl)
            && returnUrl.StartsWith('/')
            && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            && !returnUrl.StartsWith("/\\", StringComparison.Ordinal))
        {
            return returnUrl;
        }

        return "/Products";
    }
}
