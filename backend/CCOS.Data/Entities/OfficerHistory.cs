namespace CCOS.Data.Entities;

/// <summary>
/// One officer title a member held, with when it started and ended. Rows are never deleted.
/// A member has at most one open row (RevokedDate is null), which matches their current Officer row.
/// </summary>
public class OfficerHistory
{
    public int OfficerHistoryId { get; set; }
    public int MemberId { get; set; }

    /// <summary>The title, like President or Treasurer.</summary>
    public string Title { get; set; } = null!;

    public DateOnly GrantedDate { get; set; }

    /// <summary>When the title ended. Null while they still hold it.</summary>
    public DateOnly? RevokedDate { get; set; }

    /// <summary>
    /// Which admin granted the title. Not linked to a table yet: the Admins table comes in
    /// CAR-74, so this stays empty until then.
    /// </summary>
    public int? GrantedByAdminId { get; set; }

    public Member Member { get; set; } = null!;
}
