namespace CCOS.Shared;

/// <summary>
/// Identity role names. Roles are only for people who aren't members by membership rules,
/// like the club's faculty advisor; members and officers come from the Members and Officers tables.
/// </summary>
public static class ClubRoles
{
    /// <summary>
    /// The club's faculty advisor (Troy). Has the same edit power as an officer (passes the
    /// Officer policy) without needing a membership or an officer title.
    /// </summary>
    public const string Advisor = "Advisor";
}
