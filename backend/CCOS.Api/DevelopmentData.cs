using CCOS.Data;
using CCOS.Data.Entities;
using CCOS.Repositories;
using CCOS.Shared;
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
    /// <param name="services">Where the database and settings come from.</param>
    /// <param name="useLocalDatabase">True for the SQLite file, false for PostgreSQL.</param>
    /// <param name="cancellationToken">
    /// Lets the app stop the setup early (for example when it is shut down while still starting up).
    /// </param>
    public static async Task EnsureCreatedAndSeededAsync(
        IServiceProvider services,
        bool useLocalDatabase,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (useLocalDatabase)
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }
        else
        {
            // Creates or updates the tables, same as `task migrate`. Skips migrations already applied.
            await db.Database.MigrateAsync(cancellationToken);
        }

        // IgnoreQueryFilters so an inactive-only table still counts as already seeded.
        var hasProducts = await db.Products.IgnoreQueryFilters().AnyAsync(cancellationToken);
        var hasVenues = await db.Venues.AnyAsync(cancellationToken);

        // Seed venues first (events need them).
        if (!hasVenues)
        {
            await SeedVenuesAndEventsAsync(db, cancellationToken);
        }

        // Test accounts for signing in: an officer, a member and a guest (CAR-66), and the
        // advisor (CAR-67). Their shared password comes from settings (user secrets or .env), never from the code.
        // The advisor is checked on its own, so a database seeded before CAR-67 still gets one.
        var hasCustomers = await db.Customers.AnyAsync(cancellationToken);
        var hasAdvisor = await db.Users.AnyAsync(user => user.NormalizedEmail == SampleAdvisorEmail.ToUpperInvariant(), cancellationToken);

        if (!hasCustomers || !hasAdvisor)
        {
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var sampleAccountSecret = configuration[SampleAccountsSettingKey];

            if (string.IsNullOrWhiteSpace(sampleAccountSecret))
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                    .CreateLogger(typeof(DevelopmentData));
                logger.LogWarning(
                    "Sample logins were not created because {Setting} is not set. " +
                    "See \"Sample accounts\" in backend/README.md.",
                    SampleAccountsSettingKey);
            }
            else
            {
                if (!hasCustomers)
                {
                    await SeedAccountsAsync(db, sampleAccountSecret, cancellationToken);
                }

                if (!hasAdvisor)
                {
                    await SeedAdvisorAsync(db, sampleAccountSecret);
                }
            }
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

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Seeds the 3 sample venues and 4 sample events that match the frontend's sample data.
    /// Events start 7 days from now so they always stay upcoming.
    /// </summary>
    /// <param name="db">The database to add the samples to.</param>
    /// <param name="cancellationToken">Lets the app stop the setup early.</param>
    private static async Task SeedVenuesAndEventsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        // Mock venues with full addresses, so Edit and Restore work on them. The last one starts
        // out removed, so "Restore venue" can be tried right away.
        var commons = new Venue
        {
            Name = "Cougar Commons",
            Capacity = 120,
            Address = new VenueAddress { AddressLine1 = "1001 Centerpoint Dr", AddressLine2 = "Room 101", City = "Stevens Point", State = "WI", ZipCode = "54481" },
        };
        var lab = new Venue
        {
            Name = "Cyber Lab",
            Capacity = 40,
            Address = new VenueAddress { AddressLine1 = "1001 Centerpoint Dr", AddressLine2 = "Room 214", City = "Stevens Point", State = "WI", ZipCode = "54481" },
        };
        var library = new Venue
        {
            Name = "Riverside Library Meeting Room",
            Capacity = 60,
            Address = new VenueAddress { AddressLine1 = "500 River Rd", City = "Wausau", State = "WI", ZipCode = "54403-1234" },
        };
        var oldGym = new Venue
        {
            Name = "Old Gym",
            Capacity = 200,
            IsActive = false,
            Address = new VenueAddress { AddressLine1 = "250 W Grand Ave", City = "Wisconsin Rapids", State = "WI", ZipCode = "54495" },
        };

        db.Venues.AddRange(commons, lab, library, oldGym);
        await db.SaveChangesAsync(cancellationToken);

        // Upcoming events start a week from now at 5 PM club time (23:00 UTC) and last 2 hours.
        var firstStartUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(7).AddHours(23), DateTimeKind.Utc);

        var ctfNight = NewEvent(commons, "Capture the Flag Night",
            "Team up and solve security puzzles for prizes. Beginners welcome, bring a laptop.",
            firstStartUtc, capacity: 50);

        var linuxWorkshop = NewEvent(lab, "Intro to Linux Workshop",
            "Hands-on basics: the terminal, files, users and permissions.",
            firstStartUtc.AddDays(7), capacity: 30);

        // Members only, with tickets already sold (so a lower capacity can be tried on Edit).
        var hackathon = NewEvent(lab, "Members-only Hackathon",
            "An evening of building and breaking things together. Members only.",
            firstStartUtc.AddDays(14), capacity: 30, membersOnly: true);
        hackathon.TicketsSold = 12;

        var resumeNight = NewEvent(library, "Resume and LinkedIn Night",
            "Get your resume reviewed and your LinkedIn profile ready for job season.",
            firstStartUtc.AddDays(21), capacity: 40);

        // Already over: hidden from /Events and its page, but officers still see it.
        var pastMeetup = NewEvent(commons, "Fall Kickoff Meetup",
            "The first meetup of the semester.",
            firstStartUtc.AddDays(-30), capacity: 80);

        // Removed: only on the officer list, so "Restore event" can be tried right away.
        var cancelledTalk = NewEvent(library, "Guest Speaker: Careers in Security",
            "Rescheduled, details coming soon.",
            firstStartUtc.AddDays(28), capacity: 60);
        cancelledTalk.IsActive = false;

        db.Events.AddRange(ctfNight, linuxWorkshop, hackathon, resumeNight, pastMeetup, cancelledTalk);
        await db.SaveChangesAsync(cancellationToken);

        // One "General" ticket type per event, like Create Event makes.
        db.TicketTypes.AddRange(
            NewTicket(ctfNight, price: 10.00m, memberPrice: 5.00m),
            NewTicket(linuxWorkshop, price: 5.00m, memberPrice: 0.00m),
            NewTicket(hackathon, price: 0.00m, memberPrice: 0.00m),
            NewTicket(resumeNight, price: 8.00m, memberPrice: 4.00m),
            NewTicket(pastMeetup, price: 3.00m, memberPrice: 1.00m),
            NewTicket(cancelledTalk, price: 6.00m, memberPrice: 3.00m));

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A sample event that lasts 2 hours.</summary>
    private static Event NewEvent(Venue venue, string name, string description, DateTime startsAtUtc, int capacity, bool membersOnly = false) => new()
    {
        Venue = venue,
        Name = name,
        Description = description,
        StartsAtUtc = startsAtUtc,
        EndsAtUtc = startsAtUtc.AddHours(2),
        TicketCapacity = capacity,
        MembersOnly = membersOnly,
    };

    /// <summary>A sample event's "General" ticket type.</summary>
    private static TicketType NewTicket(Event @event, decimal price, decimal memberPrice) => new()
    {
        Event = @event,
        Category = "Tickets",
        Name = "General",
        Price = price,
        MemberPrice = memberPrice,
        StockQuantity = 0,
    };

    /// <summary>
    /// The setting that holds the shared password for the sample accounts. Set it in user
    /// secrets (dotnet run) or as SAMPLE_ACCOUNT_PASSWORD in .env (Docker); see backend/README.md.
    /// It's kept out of the code so no password is ever committed.
    /// </summary>
    public const string SampleAccountsSettingKey = "SampleAccounts:Password";

    /// <summary>The sample advisor's email (backend/README.md, "Sample accounts").</summary>
    private const string SampleAdvisorEmail = "advisor@ccos.test";

    /// <summary>
    /// Seeds three sample people, each with a login (backend/README.md, "Sample accounts"):
    /// officer@ccos.test is a current member who holds the President title,
    /// member@ccos.test is a current member, and guest@ccos.test has a login but no membership.
    /// Memberships run one year from today (the club's date).
    /// </summary>
    private static async Task SeedAccountsAsync(
        AppDbContext db,
        string sampleAccountSecret,
        CancellationToken cancellationToken)
    {
        var today = new ClubClock().Today;

        var officerCustomer = new Customer { FirstName = "Officer", LastName = "Sample" };
        var memberCustomer = new Customer { FirstName = "Member", LastName = "Sample" };
        var guestCustomer = new Customer { FirstName = "Guest", LastName = "Sample" };

        db.Customers.AddRange(officerCustomer, memberCustomer, guestCustomer);

        // Save first so each customer gets its CustomerId, which the logins and memberships reuse.
        await db.SaveChangesAsync(cancellationToken);

        db.Users.AddRange(
            CreateSampleLogin("officer@ccos.test", officerCustomer, sampleAccountSecret),
            CreateSampleLogin("member@ccos.test", memberCustomer, sampleAccountSecret),
            CreateSampleLogin("guest@ccos.test", guestCustomer, sampleAccountSecret));

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

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Seeds the sample advisor: a person with a login and the Advisor role, but no membership
    /// or officer title, who still passes the Officer policy (CAR-67). Creates the Advisor role
    /// first if the database doesn't have it yet.
    /// </summary>
    private static async Task SeedAdvisorAsync(AppDbContext db, string sampleAccountSecret)
    {
        var advisorRole = await db.Roles.SingleOrDefaultAsync(role => role.NormalizedName == ClubRoles.Advisor.ToUpperInvariant());
        if (advisorRole is null)
        {
            advisorRole = new IdentityRole(ClubRoles.Advisor) { NormalizedName = ClubRoles.Advisor.ToUpperInvariant() };
            db.Roles.Add(advisorRole);
        }

        // Every login needs a customer, so save the advisor's first to get its CustomerId.
        var advisorCustomer = new Customer { FirstName = "Advisor", LastName = "Sample" };
        db.Customers.Add(advisorCustomer);
        await db.SaveChangesAsync();

        var advisor = CreateSampleLogin(SampleAdvisorEmail, advisorCustomer, sampleAccountSecret);
        db.Users.Add(advisor);
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = advisor.Id, RoleId = advisorRole.Id });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Builds a sign-in account for a sample customer, with the email as the user name and the
    /// shared sample password hashed the same way Identity hashes real passwords.
    /// </summary>
    private static AppUser CreateSampleLogin(string email, Customer customer, string sampleAccountSecret)
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

        user.PasswordHash = new PasswordHasher<AppUser>().HashPassword(user, sampleAccountSecret);
        return user;
    }
}
