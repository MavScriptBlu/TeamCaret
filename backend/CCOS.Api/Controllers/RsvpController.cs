using System.Security.Claims;
using CCOS.Repositories;
using CCOS.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CCOS.Api.Controllers;

/// <summary>
/// RSVP endpoints (CAR-94) for the signed-in customer: see whether they're going, RSVP to a free
/// event (one ticket through a normal order, so it holds a seat), and cancel the RSVP (the seat goes
/// back). Membership is checked fresh on every request, the same rule as orders.
/// </summary>
[ApiController]
[Route("api/events/{eventId:int}/rsvp")]
[Authorize]
public class RsvpController(
    RsvpRepository rsvps,
    AccountRepository accounts,
    MemberRepository members,
    ClubClock clock) : ControllerBase
{
    /// <summary>GET /api/events/{eventId}/rsvp: whether the signed-in customer is going. 401 when not signed in.</summary>
    [HttpGet]
    public async Task<ActionResult<RsvpStatusDto>> Status(int eventId, CancellationToken cancellationToken)
    {
        int? customerId = await CustomerIdAsync(cancellationToken);
        if (customerId is null)
        {
            return Unauthorized();
        }

        return ToDto(await rsvps.GetStatusAsync(customerId.Value, eventId, cancellationToken));
    }

    /// <summary>
    /// POST /api/events/{eventId}/rsvp: RSVP to a free event. 200 with the status (also when already
    /// going), 404 for an unknown event, 403 for members only, 409 when it isn't free, is sold out or
    /// has started (the "detail" sentence says which).
    /// </summary>
    [HttpPost]
    // No anti-forgery check on purpose: the API only takes bearer tokens, never a sign-in
    // cookie (CAR-83), so a forged cross-site request has nothing to ride on.
    [IgnoreAntiforgeryToken]
    public async Task<ActionResult<RsvpStatusDto>> Rsvp(int eventId, CancellationToken cancellationToken)
    {
        int? customerId = await CustomerIdAsync(cancellationToken);
        if (customerId is null)
        {
            return Unauthorized();
        }

        bool isCurrentMember = await members.IsCurrentMemberAsync(customerId.Value, clock.Today, cancellationToken);
        RsvpResult result = await rsvps.RsvpAsync(customerId.Value, isCurrentMember, eventId, cancellationToken);

        return result.Outcome switch
        {
            RsvpOutcome.Going or RsvpOutcome.AlreadyGoing =>
                ToDto(await rsvps.GetStatusAsync(customerId.Value, eventId, cancellationToken)),
            RsvpOutcome.NotFound => Problem(title: "Not found", detail: result.Message, statusCode: StatusCodes.Status404NotFound),
            RsvpOutcome.MembersOnly => Problem(title: "Members only", detail: result.Message, statusCode: StatusCodes.Status403Forbidden),
            RsvpOutcome.NotFree => Problem(title: "Not free", detail: result.Message, statusCode: StatusCodes.Status409Conflict),
            _ => Problem(title: "Not available", detail: result.Message, statusCode: StatusCodes.Status409Conflict),
        };
    }

    /// <summary>
    /// DELETE /api/events/{eventId}/rsvp: cancel the RSVP and give the seat back. 204 when canceled,
    /// 404 when there was no free RSVP to cancel.
    /// </summary>
    [HttpDelete]
    // Bearer tokens only (CAR-83), so no anti-forgery check; see Rsvp above.
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Cancel(int eventId, CancellationToken cancellationToken)
    {
        int? customerId = await CustomerIdAsync(cancellationToken);
        if (customerId is null)
        {
            return Unauthorized();
        }

        int canceled = await rsvps.CancelAsync(customerId.Value, eventId, cancellationToken);
        return canceled == 0
            ? Problem(title: "No RSVP", detail: "You don't have an RSVP to cancel for this event.", statusCode: StatusCodes.Status404NotFound)
            : NoContent();
    }

    // The token carries the login id; every login has a customer, so null means the login is gone.
    private async Task<int?> CustomerIdAsync(CancellationToken cancellationToken)
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null ? null : await accounts.FindCustomerIdAsync(userId, cancellationToken);
    }

    private static RsvpStatusDto ToDto(RsvpStatus status) => new(status.Tickets > 0, status.Tickets, status.FreeTickets > 0);
}
