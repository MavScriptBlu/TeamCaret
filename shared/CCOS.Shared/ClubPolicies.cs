namespace CCOS.Shared;

/// <summary>
/// Names of the two access rules (authorization policies) from docs/CAR-52_Login_Approach.md.
/// The API defines what each one checks; anything that protects a page or endpoint uses these names.
/// </summary>
public static class ClubPolicies
{
    /// <summary>Signed in and a current member (active, dues paid through today).</summary>
    public const string Member = "Member";

    /// <summary>Signed in, a current member, and holding an officer title.</summary>
    public const string Officer = "Officer";
}
