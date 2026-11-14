namespace CCOS.Data.Entities;

/// <summary>
/// A person the club knows about: anyone who has an account, whether or not they're a member.
/// Customers are never deleted, so their orders, membership and officer history always have someone to point at.
/// </summary>
public class Customer
{
    public int CustomerId { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;

    /// <summary>Their membership, if they've ever been a member. Null for a guest.</summary>
    public Member? Member { get; set; }
}
