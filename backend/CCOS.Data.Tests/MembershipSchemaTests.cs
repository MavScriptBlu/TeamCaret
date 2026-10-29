using CCOS.Data;
using CCOS.Data.Entities;
using CCOS.Repositories;
using CCOS.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CCOS.Data.Tests;

/// <summary>
/// Tests for the membership and login tables (CAR-28, CAR-66). "Model" tests check how
/// AppDbContext is set up; "behavior" tests run MemberRepository against a throwaway in-memory
/// SQLite database to prove the current-member and current-officer rules.
/// </summary>
public class MembershipSchemaTests
{
    // A fixed club date, so the tests never depend on the real calendar.
    private static readonly DateOnly Today = new(2026, 10, 28);

    // ─────────────────────────────────────────────────────────────────────────
    // Model
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Checks the CAR-28 "mapping checks": member and officer ids aren't generated, every
    /// relationship blocks deletes, members have no query filter, and the open-title and
    /// one-login-per-customer indexes exist.
    /// </summary>
    [Fact]
    public async Task Model_builds_membership_tables_with_expected_rules()
    {
        await using var connection = await CreateConnectionAsync();
        await using var db = new AppDbContext(BuildOptions(connection));
        var model = db.GetService<IDesignTimeModel>().Model;
        var memberEntity = model.FindEntityType(typeof(Member))!;
        var officerEntity = model.FindEntityType(typeof(Officer))!;
        var historyEntity = model.FindEntityType(typeof(OfficerHistory))!;
        var userEntity = model.FindEntityType(typeof(AppUser))!;

        // MemberId is the CustomerId and OfficerId is the MemberId, so the database must not make them up.
        Assert.Equal(ValueGenerated.Never, memberEntity.FindProperty(nameof(Member.MemberId))!.ValueGenerated);
        Assert.Equal(ValueGenerated.Never, officerEntity.FindProperty(nameof(Officer.OfficerId))!.ValueGenerated);

        // Status is worked out, never stored, and members are never hidden by a query filter.
        Assert.Null(memberEntity.FindProperty("Status"));
        Assert.Null(memberEntity.FindProperty("IsCurrent"));
        Assert.Empty(memberEntity.GetDeclaredQueryFilters());
        Assert.Equal(true, memberEntity.FindProperty(nameof(Member.IsActive))!.GetDefaultValue());

        // Every relationship in the model blocks deletes, including Identity's own tables.
        Assert.All(
            model.GetEntityTypes().SelectMany(entity => entity.GetForeignKeys()),
            foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));

        // At most one open officer title per member.
        var openTitleIndex = historyEntity.GetIndexes()
            .Single(index => index.GetDatabaseName() == "UX_OfficerHistory_MemberId_Open");
        Assert.True(openTitleIndex.IsUnique);
        Assert.Equal("\"RevokedDate\" IS NULL", openTitleIndex.GetFilter());

        // One login per customer.
        var customerIndex = userEntity.GetIndexes()
            .Single(index => index.GetDatabaseName() == "UX_AspNetUsers_CustomerId");
        Assert.True(customerIndex.IsUnique);

        // The admin columns exist but aren't linked to a table yet (Admins comes in CAR-74).
        Assert.DoesNotContain(
            memberEntity.GetForeignKeys(),
            foreignKey => foreignKey.Properties.Any(property => property.Name == nameof(Member.DeactivatedByAdminId)));
        Assert.DoesNotContain(
            officerEntity.GetForeignKeys(),
            foreignKey => foreignKey.Properties.Any(property => property.Name == nameof(Officer.GrantedByAdminId)));
    }

    /// <summary>
    /// On PostgreSQL, membership dates are stored as date (no time), per CAR-28.
    /// Only builds the model, so it needs no running database.
    /// </summary>
    [Fact]
    public void Postgres_model_stores_membership_dates_as_date()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=ccos;Username=ccos;Password=not-used")
            .Options;

        using var db = new AppDbContext(options);
        var model = db.GetService<IDesignTimeModel>().Model;

        Assert.Equal("date", ColumnType<Member>(model, nameof(Member.MemberExpiration)));
        Assert.Equal("date", ColumnType<Member>(model, nameof(Member.JoinedDate)));
        Assert.Equal("date", ColumnType<Member>(model, nameof(Member.DeactivatedDate)));
        Assert.Equal("date", ColumnType<Officer>(model, nameof(Officer.GrantedDate)));
        Assert.Equal("date", ColumnType<OfficerHistory>(model, nameof(OfficerHistory.GrantedDate)));
        Assert.Equal("date", ColumnType<OfficerHistory>(model, nameof(OfficerHistory.RevokedDate)));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Status
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// StatusOn: paid through today or later is Active, a passed date is Expired, and
    /// IsActive = false is Inactive even when the dues date is still in the future.
    /// </summary>
    [Theory]
    [InlineData(true, 1, MemberStatus.Active)]
    [InlineData(true, 0, MemberStatus.Active)]
    [InlineData(true, -1, MemberStatus.Expired)]
    [InlineData(false, 30, MemberStatus.Inactive)]
    [InlineData(false, -30, MemberStatus.Inactive)]
    public void StatusOn_works_out_status_from_IsActive_and_expiration(bool isActive, int daysUntilExpiration, MemberStatus expected)
    {
        var member = new Member { IsActive = isActive, MemberExpiration = Today.AddDays(daysUntilExpiration) };

        Assert.Equal(expected, member.StatusOn(Today));
    }

    /// <summary>
    /// The club's date is America/Chicago, not UTC: 10 p.m. Central on Oct 31 is already
    /// Nov 1 in UTC, but it's still Oct 31 for the club.
    /// </summary>
    [Fact]
    public void ClubClock_uses_the_club_time_zone()
    {
        var lateEveningCentral = new DateTime(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateOnly(2026, 10, 31), ClubClock.ToClubDate(lateEveningCentral));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Current member and current officer
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A member counts as current through their expiration date. Expired, inactive and
    /// guest (no member row) customers do not.
    /// </summary>
    [Fact]
    public async Task IsCurrentMember_follows_the_CAR28_rules()
    {
        await using var connection = await CreateConnectionAsync();
        await using var db = new AppDbContext(BuildOptions(connection));
        await db.Database.EnsureCreatedAsync();

        var paidUp = await AddMemberAsync(db, isActive: true, expiration: Today.AddMonths(6));
        var lastDay = await AddMemberAsync(db, isActive: true, expiration: Today);
        var expired = await AddMemberAsync(db, isActive: true, expiration: Today.AddDays(-1));
        var left = await AddMemberAsync(db, isActive: false, expiration: Today.AddMonths(6));
        var guest = await AddCustomerAsync(db, "Guest");

        var repository = new MemberRepository(db);

        Assert.True(await repository.IsCurrentMemberAsync(paidUp.MemberId, Today));
        Assert.True(await repository.IsCurrentMemberAsync(lastDay.MemberId, Today));
        Assert.False(await repository.IsCurrentMemberAsync(expired.MemberId, Today));
        Assert.False(await repository.IsCurrentMemberAsync(left.MemberId, Today));
        Assert.False(await repository.IsCurrentMemberAsync(guest.CustomerId, Today));
    }

    /// <summary>
    /// An officer must also be a current member. Expired dues pause officer access, leaving
    /// the club ends it, and a current member without a title isn't an officer.
    /// </summary>
    [Fact]
    public async Task IsCurrentOfficer_requires_a_title_and_a_current_membership()
    {
        await using var connection = await CreateConnectionAsync();
        await using var db = new AppDbContext(BuildOptions(connection));
        await db.Database.EnsureCreatedAsync();

        var currentOfficer = await AddMemberAsync(db, isActive: true, expiration: Today.AddMonths(6), title: "President");
        var expiredOfficer = await AddMemberAsync(db, isActive: true, expiration: Today.AddDays(-1), title: "Treasurer");
        var leftOfficer = await AddMemberAsync(db, isActive: false, expiration: Today.AddMonths(6), title: "Secretary");
        var plainMember = await AddMemberAsync(db, isActive: true, expiration: Today.AddMonths(6));

        var repository = new MemberRepository(db);

        Assert.True(await repository.IsCurrentOfficerAsync(currentOfficer.MemberId, Today));
        Assert.False(await repository.IsCurrentOfficerAsync(expiredOfficer.MemberId, Today));
        Assert.False(await repository.IsCurrentOfficerAsync(leftOfficer.MemberId, Today));
        Assert.False(await repository.IsCurrentOfficerAsync(plainMember.MemberId, Today));
    }

    /// <summary>
    /// The database refuses a second open officer title for the same member, but a closed
    /// past title next to an open one is fine.
    /// </summary>
    [Fact]
    public async Task OfficerHistory_allows_only_one_open_title_per_member()
    {
        await using var connection = await CreateConnectionAsync();
        await using var db = new AppDbContext(BuildOptions(connection));
        await db.Database.EnsureCreatedAsync();

        var member = await AddMemberAsync(db, isActive: true, expiration: Today.AddMonths(6));

        // A closed past title and one open title: allowed.
        db.OfficerHistory.AddRange(
            new OfficerHistory { MemberId = member.MemberId, Title = "Secretary", GrantedDate = Today.AddYears(-1), RevokedDate = Today.AddDays(-1) },
            new OfficerHistory { MemberId = member.MemberId, Title = "President", GrantedDate = Today });
        await db.SaveChangesAsync();

        // A second open title: refused.
        db.OfficerHistory.Add(new OfficerHistory { MemberId = member.MemberId, Title = "Treasurer", GrantedDate = Today });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a customer with a member row, and optionally a current officer title, and returns the member.
    /// </summary>
    private static async Task<Member> AddMemberAsync(AppDbContext db, bool isActive, DateOnly expiration, string? title = null)
    {
        var customer = await AddCustomerAsync(db, title ?? "Member");

        var member = new Member
        {
            MemberId = customer.CustomerId,
            IsActive = isActive,
            JoinedDate = Today.AddYears(-1),
            MemberExpiration = expiration,
        };
        db.Members.Add(member);

        if (title is not null)
        {
            db.Officers.Add(new Officer { OfficerId = customer.CustomerId, Title = title, GrantedDate = Today.AddMonths(-1) });
        }

        await db.SaveChangesAsync();
        return member;
    }

    /// <summary>Adds and saves a customer, so it gets its CustomerId.</summary>
    private static async Task<Customer> AddCustomerAsync(AppDbContext db, string lastName)
    {
        var customer = new Customer { FirstName = "Test", LastName = lastName };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer;
    }

    /// <summary>The database column type EF will use for one property.</summary>
    private static string ColumnType<TEntity>(IModel model, string propertyName) =>
        model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetRelationalTypeMapping().StoreType;

    private static DbContextOptions<AppDbContext> BuildOptions(SqliteConnection connection) =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

    private static async Task<SqliteConnection> CreateConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }
}
