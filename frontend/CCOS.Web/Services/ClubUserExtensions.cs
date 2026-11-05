using System.Security.Claims;

namespace CCOS.Web.Services;

/// <summary>
/// Quick checks on the signed-in person for pages and the layout (CAR-69), read from the sign-in
/// cookie. Use them to show or hide links; the API's own check is the real guard.
/// </summary>
public static class ClubUserExtensions
{
    /// <summary>True for a current officer or the club's advisor.</summary>
    public static bool IsOfficer(this ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && user.HasClaim(AccountSession.OfficerClaim, "true");

    /// <summary>True for a current member (dues paid through today).</summary>
    public static bool IsCurrentMember(this ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && user.HasClaim(AccountSession.MemberClaim, "true");
}
