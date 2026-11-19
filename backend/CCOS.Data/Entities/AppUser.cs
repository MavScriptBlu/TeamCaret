using Microsoft.AspNetCore.Identity;

namespace CCOS.Data.Entities;

/// <summary>
/// A login account (ASP.NET Core Identity). Identity stores the email and password hash;
/// CustomerId links the login to the person it belongs to (docs/CAR-52_Login_Approach.md, section 3).
/// Whether they're a member or officer is read from the Members and Officers tables, never from the login.
/// </summary>
public class AppUser : IdentityUser
{
    /// <summary>The customer this login belongs to. Required and unique: one login per customer.</summary>
    public int CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;
}
