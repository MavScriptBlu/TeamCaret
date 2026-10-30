namespace CCOS.Shared;

/// <summary>
/// What GET /api/me returns about the signed-in person (CAR-67). Member and officer status are
/// worked out on every request from the Members and Officers tables on the club's date, so a
/// renewal or an expired membership shows up right away.
/// </summary>
/// <param name="CustomerId">The person's CustomerId.</param>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
/// <param name="Email">The email they sign in with.</param>
/// <param name="MemberStatus">Active, Expired or Inactive; null for a guest who has never been a member.</param>
/// <param name="IsCurrentMember">True when they get member prices and members-only events today.</param>
/// <param name="IsCurrentOfficer">True when they hold an officer title and are a current member.</param>
/// <param name="OfficerTitle">Their officer title (like President), or null if they don't hold one.</param>
public record CurrentAccountDto(
    int CustomerId,
    string FirstName,
    string LastName,
    string Email,
    MemberStatus? MemberStatus,
    bool IsCurrentMember,
    bool IsCurrentOfficer,
    string? OfficerTitle);
