using CCOS.Data;
using Microsoft.EntityFrameworkCore;

namespace CCOS.Repositories;

/// <summary>
/// Answers "is this person a current member?" and "is this person a current officer?" (CAR-28).
/// Every check takes the club's date as a parameter (from ClubClock), so all checks agree on
/// "today" and tests can pass any date. Nothing is cached: a membership change counts on the next check.
/// </summary>
public sealed class MemberRepository(AppDbContext db)
{
    /// <summary>
    /// True when the customer has a member row that is active and paid through today or later.
    /// No row, an inactive row or an expired row all mean "not a member right now".
    /// Used for member pricing and members-only events.
    /// </summary>
    public Task<bool> IsCurrentMemberAsync(int customerId, DateOnly today, CancellationToken cancellationToken = default)
    {
        return db.Members.AnyAsync(
            member => member.MemberId == customerId
                && member.IsActive
                && member.MemberExpiration >= today,
            cancellationToken);
    }

    /// <summary>
    /// True when the customer holds an officer title AND is a current member. An officer whose dues
    /// expired keeps the title, but their officer access pauses until they renew.
    /// Used for the officer-only pages and endpoints.
    /// </summary>
    public Task<bool> IsCurrentOfficerAsync(int customerId, DateOnly today, CancellationToken cancellationToken = default)
    {
        return db.Officers.AnyAsync(
            officer => officer.OfficerId == customerId
                && officer.Member.IsActive
                && officer.Member.MemberExpiration >= today,
            cancellationToken);
    }
}
