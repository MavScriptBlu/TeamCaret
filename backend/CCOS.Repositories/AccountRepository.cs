using CCOS.Data;
using CCOS.Data.Entities;
using CCOS.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CCOS.Repositories;

/// <summary>
/// Login accounts and the person behind them (CAR-67). Registering creates the Customer and the
/// AppUser together, and the profile query reads member and officer status fresh every time.
/// </summary>
public sealed class AccountRepository(AppDbContext db, UserManager<AppUser> userManager)
{
    /// <summary>
    /// Creates a customer and their login in ONE transaction: if the login can't be created
    /// (email already used, password too weak), the customer row is rolled back too, so there's
    /// never a customer left behind without a login, or a login without a customer.
    /// </summary>
    public async Task<AccountRegistration> RegisterAsync(
        string firstName,
        string lastName,
        string email,
        string newAccountSecret,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Save the customer first so the login can point at its CustomerId.
        var customer = new Customer { FirstName = firstName.Trim(), LastName = lastName.Trim() };
        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);

        // UserManager uses the same AppDbContext, so this save is part of the same transaction.
        var trimmedEmail = email.Trim();
        var user = new AppUser { UserName = trimmedEmail, Email = trimmedEmail, CustomerId = customer.CustomerId };
        var result = await userManager.CreateAsync(user, newAccountSecret);

        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AccountRegistration(false, null, result.Errors.ToList());
        }

        await transaction.CommitAsync(cancellationToken);
        return new AccountRegistration(true, customer.CustomerId, []);
    }

    /// <summary>
    /// The CustomerId behind a login (the user id from the sign-in token), or null if the login doesn't exist.
    /// </summary>
    public async Task<int?> FindCustomerIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await db.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => (int?)user.CustomerId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// True when the login has the Advisor role. Read from the database every time, so adding or
    /// removing the role counts on the next request.
    /// </summary>
    public Task<bool> IsAdvisorAsync(string userId, CancellationToken cancellationToken = default)
    {
        var advisorRole = ClubRoles.Advisor.ToUpperInvariant();
        return db.UserRoles.AnyAsync(
            userRole => userRole.UserId == userId
                && db.Roles.Any(role => role.Id == userRole.RoleId && role.NormalizedName == advisorRole),
            cancellationToken);
    }

    /// <summary>
    /// Who's signed in, with their member and officer status on the given club date (CAR-28 rules:
    /// an officer must also be a current member). Null if the login doesn't exist.
    /// </summary>
    public async Task<CurrentAccountDto?> GetProfileAsync(
        string userId,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var row = await db.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new
            {
                user.CustomerId,
                user.Email,
                user.Customer.FirstName,
                user.Customer.LastName,
                user.Customer.Member,
                OfficerTitle = user.Customer.Member != null && user.Customer.Member.Officer != null
                    ? user.Customer.Member.Officer.Title
                    : null,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var status = row.Member?.StatusOn(today);
        var isCurrentMember = status == MemberStatus.Active;
        var isAdvisor = await IsAdvisorAsync(userId, cancellationToken);

        return new CurrentAccountDto(
            row.CustomerId,
            row.FirstName,
            row.LastName,
            row.Email ?? string.Empty,
            status,
            isCurrentMember,
            isCurrentMember && row.OfficerTitle is not null,
            row.OfficerTitle,
            isAdvisor);
    }
}

/// <summary>
/// The result of RegisterAsync: whether it worked, the new CustomerId, and Identity's error
/// list when it didn't (for example DuplicateEmail or PasswordTooShort).
/// </summary>
public sealed record AccountRegistration(bool Succeeded, int? CustomerId, IReadOnlyList<IdentityError> Errors);
