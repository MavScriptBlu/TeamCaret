using Microsoft.AspNetCore.Authorization;

namespace CCOS.Api.Authorization;

/// <summary>
/// The rule behind the Member and Officer policies (docs/CAR-52_Login_Approach.md, section 4):
/// the signed-in person must be a current member, and for Officer must also hold an officer title.
/// </summary>
public sealed class MembershipRequirement(bool officerRequired) : IAuthorizationRequirement
{
    /// <summary>True for the Officer policy, false for the Member policy.</summary>
    public bool OfficerRequired { get; } = officerRequired;
}
