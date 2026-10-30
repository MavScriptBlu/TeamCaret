using System.Security.Claims;
using CCOS.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace CCOS.Api.Authorization;

/// <summary>
/// Checks a MembershipRequirement on every request: finds the CustomerId behind the signed-in
/// login, then asks MemberRepository whether they're a current member (or current officer) on
/// the club's date. Nothing about membership is stored in the sign-in token, so renewals,
/// expirations and leaving the club take effect on the very next request.
/// </summary>
public sealed class MembershipAuthorizationHandler(
    AccountRepository accounts,
    MemberRepository members,
    ClubClock clock) : AuthorizationHandler<MembershipRequirement>
{
    /// <summary>
    /// Succeeds only when the rule passes. Doing nothing means "not allowed": the API then answers
    /// 401 when nobody is signed in, or 403 when someone is signed in but doesn't qualify.
    /// </summary>
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MembershipRequirement requirement)
    {
        // The sign-in token carries the login's user id, not the CustomerId.
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return;
        }

        var customerId = await accounts.FindCustomerIdAsync(userId);
        if (customerId is null)
        {
            return;
        }

        var today = clock.Today;
        var allowed = requirement.OfficerRequired
            ? await members.IsCurrentOfficerAsync(customerId.Value, today)
            : await members.IsCurrentMemberAsync(customerId.Value, today);

        if (allowed)
        {
            context.Succeed(requirement);
        }
    }
}
