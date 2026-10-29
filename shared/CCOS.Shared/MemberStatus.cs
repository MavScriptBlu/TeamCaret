namespace CCOS.Shared;

/// <summary>
/// A member's status (CAR-28). Never stored in the database: it's worked out from
/// Members.IsActive and Members.MemberExpiration on the club's date.
/// Shared so the website can show it.
/// </summary>
public enum MemberStatus
{
    /// <summary>In the club and dues are paid through today or later.</summary>
    Active,

    /// <summary>Marked by an admin as having left the club.</summary>
    Inactive,

    /// <summary>Still in the club, but the dues date has passed.</summary>
    Expired,
}
