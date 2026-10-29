using CCOS.Data;
using CCOS.Data.Entities;


using CCOS.Repositories;
using Microsoft.AspNetCore.Identity;

using Microsoft.EntityFrameworkCore;

namespace CCOS.Api;

/// <summary>
/// Development sample data for both local databases:
/// a SQLite file when no ConnectionStrings:CCOS is set, or PostgreSQL in Docker when it is.
/// Only runs in Development, so sample products never reach a real database.
/// </summary>
public static class DevelopmentData
{
    /// <summary>
    /// Makes sure the database has its tables, then adds sample products the first time.
    /// SQLite builds its tables directly. PostgreSQL applies the EF Core migrations.
    /// The samples cover regular and member pricing, out of stock and an inactive product
    /// (see docs/Product_Migration_Scope.md).
    /// </summary>
    public static async Task EnsureCreatedAndSeededAsync(IServiceProvider services, bool useLocalDatabase)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (useLocalDatabase)
        {
            await db.Database.EnsureCreatedAsync();
        }
        else
        {
            // Creates or updates the tables, same as `task migrate`. Skips migrations already applied.
            await db.Database.MigrateAsync();
        }

        // IgnoreQueryFilters so an inactive-only table still counts as already seeded.
        var hasProducts = await db.Products.IgnoreQueryFilters().AnyAsync();
        var hasVenues = await db.Venues.AnyAsync();

        // Seed venues first (events need them).
        if (!hasVenues)
        {
            await SeedVenuesAndEventsAsync(db);
        }



        // Test accounts for signing in (CAR-66): one officer, one member, one guest.
        if (!await db.Customers.AnyAsync())
        {
            await SeedAccountsAsync(db);
        }


        if (hasProducts)
        {
            return;
        }

        // The hoodie goes first so it gets ProductId 1, matching the sample on Products/Details.
        db.Products.AddRange(
            new Product
            {
                Category = "Swag",
                Name = "Club Hoodie",
                Description = "A comfortable Cyber Cougars hoodie for showing your club pride.",
                Price = 35.00m,
                MemberPrice = 28.00m,
                StockQuantity = 42,
            },
            new Product
            {
                Category = "Swag",
                Name = "Club T-Shirt",
                Description = "Cyber Cougars logo tee.",
                Price = 15.00m,
                MemberPrice = 12.00m,
                StockQuantity = 30,
            },
            new Product
            {
                Category = "Swag",
                Name = "Sticker Pack",
                Description = "A set of club stickers.",
                Price = 5.00m,
                MemberPrice = 3.00m,
                StockQuantity = 0,
            },
            new Product
            {
                Category = "Resources",
                Name = "Study Guide",
                Description = "Printed study guide for club workshops.",
                Price = 8.00m,
                MemberPrice = 5.00m,
                StockQuantity = 12,
            },
            new Product
            {
                Category = "Swag",
                Name = "Old Mug",
                Description = "Retired product, kept for order history.",
                Price = 10.00m,
                MemberPrice = 8.00m,
                StockQuantity = 5,
                IsActive = false,
            });

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds the 3 sample venues and 4 sample events that match the frontend's sample data.
    /// Events start 7 days from now so they always stay upcoming.
    /// </summary>
    private static async Task SeedVenuesAndEventsAsync(AppDbContext db)
    {
        // The 3 sample venues the pages show.
        var venue1 = new Venue { Name = "Sample Venue, Room 101", Capacity = 100 };
        var venue2 = new Venue { Name = "Sample Venue, Room 102", Capacity = 80 };
        var venue3 = new Venue { Name = "Sample Venue, Room 103", Capacity = 60 };

        db.Venues.AddRange(venue1, venue2, venue3);
        await db.SaveChangesAsync();

        // Events start a week from now and last 2 hours, same as the frontend samples.
        var firstStartUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(7).AddHours(23), DateTimeKind.Utc);

        var event1 = new Event
        {
            Venue = venue1,
            Name = "Sample Event 1",
            Description = "Description and further details about the event.",
            StartsAtUtc = firstStartUtc,
            EndsAtUtc = firstStartUtc.AddHours(2),
            TicketCapacity = 50,
            MembersOnly = false
        };

        var event2 = new Event
        {
            Venue = venue2,
            Name = "Sample Event 2",
            Description = "Description and further details about the event.",
            StartsAtUtc = firstStartUtc.AddDays(7),
            EndsAtUtc = firstStartUtc.AddDays(7).AddHours(2),
            TicketCapacity = 40,
            MembersOnly = false
        };

        var event3 = new Event
        {
            Venue = venue3,
            Name = "Sample Event 3",
            Description = "Description and further details about the event.",
            StartsAtUtc = firstStartUtc.AddDays(14),
            EndsAtUtc = firstStartUtc.AddDays(14).AddHours(2),
            TicketCapacity = 30,
            MembersOnly = true
        };

        var event4 = new Event
        {
            Venue = venue1,
            Name = "Sample Event 4",
            Description = "Description and further details about the event.",
            StartsAtUtc = firstStartUtc.AddDays(21),
            EndsAtUtc = firstStartUtc.AddDays(21).AddHours(2),
            TicketCapacity = 50,
            MembersOnly = false
        };

        db.Events.AddRange(event1, event2, event3, event4);
        await db.SaveChangesAsync();

        // One ticket type per event, matching the frontend samples.
        db.TicketTypes.AddRange(
            new TicketType
            {
                Event = event1,
                Category = "Tickets",
                Name = "General",
                Price = 10.00m,
                MemberPrice = 5.00m,
                StockQuantity = 0
            },
            new TicketType
            {
                Event = event2,
                Category = "Tickets",
                Name = "General",
                Price = 5.00m,
                MemberPrice = 0.00m,
                StockQuantity = 0
            },
            new TicketType
            {
                Event = event3,
                Category = "Tickets",
                Name = "General",
                Price = 0.00m,
                MemberPrice = 0.00m,
                StockQuantity = 0
            },
            new TicketType
            {
                Event = event4,
                Category = "Tickets",
                Name = "General",
                Price = 8.00m,
                MemberPrice = 4.00m,
                StockQuantity = 0
            });

        await db.SaveChangesAsync();
    }



    /// <summary>
    /// The shared password for the sample accounts. Development only, and listed in
    /// backend/README.md so everyone can sign in. Never use it for a real account.
    /// </summary>
    public const string SampleAccountPassword = "CcosDev!2026";

    /// <summary>
    /// Seeds three sample people, each with a login (backend/README.md, "Sample accounts"):
    /// officer@ccos.test is a current member who holds the President title,
    /// member@ccos.test is a current member, and guest@ccos.test has a login but no membership.
    /// Memberships run one year from today (the club's date).
    /// </summary>
    private static async Task SeedAccountsAsync(AppDbContext db)
    {
        var today = new ClubClock().Today;

        var officerCustomer = new Customer { FirstName = "Sample", LastName = "Officer" };
        var memberCustomer = new Customer { FirstName = "Sample", LastName = "Member" };
        var guestCustomer = new Customer { FirstName = "Sample", LastName = "Guest" };

        db.Customers.AddRange(officerCustomer, memberCustomer, guestCustomer);

        // Save first so each customer gets its CustomerId, which the logins and memberships reuse.
        await db.SaveChangesAsync();

        db.Users.AddRange(
            CreateSampleLogin("officer@ccos.test", officerCustomer),
            CreateSampleLogin("member@ccos.test", memberCustomer),
            CreateSampleLogin("guest@ccos.test", guestCustomer));

        // MemberId is the CustomerId. The guest has no member row.
        db.Members.AddRange(
            new Member
            {
                MemberId = officerCustomer.CustomerId,
                JoinedDate = today,
                MemberExpiration = today.AddYears(1),
            },
            new Member
            {
                MemberId = memberCustomer.CustomerId,
                JoinedDate = today,
                MemberExpiration = today.AddYears(1),
            });

        // The officer's current title, plus the matching open history row.
        // GrantedByAdminId stays empty until the Admins table exists (CAR-74).
        db.Officers.Add(new Officer
        {
            OfficerId = officerCustomer.CustomerId,
            Title = "President",
            GrantedDate = today,
        });
        db.OfficerHistory.Add(new OfficerHistory
        {
            MemberId = officerCustomer.CustomerId,
            Title = "President",
            GrantedDate = today,
        });

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Builds a sign-in account for a sample customer, with the email as the user name and the
    /// shared sample password hashed the same way Identity hashes real passwords.
    /// </summary>
    private static AppUser CreateSampleLogin(string email, Customer customer)
    {
        var user = new AppUser
        {
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            CustomerId = customer.CustomerId,
        };

        user.PasswordHash = new PasswordHasher<AppUser>().HashPassword(user, SampleAccountPassword);
        return user;
    }

}
