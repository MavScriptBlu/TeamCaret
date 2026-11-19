/// <summary>
/// The application's database context, representing the session with the database.
/// Contains DbSet properties for each entity type.
/// </summary>
using CCOS.Data.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CCOS.Data;

/// <summary>
/// The app's connection to the database. Each DbSet below is one table, and OnModelCreating
/// describes the rules for every table (column sizes, required fields, check constraints,
/// relationships, indexes). Migrations are built from what is written here.
/// It's an IdentityDbContext, so ASP.NET Core Identity's login tables (AspNetUsers and friends)
/// live in the same database and migration history (docs/CAR-52_Login_Approach.md).
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    // Products and TicketTypes share one table (see the Product setup below).
    public DbSet<Product> Products => Set<Product>();
    public DbSet<TicketType> TicketTypes => Set<TicketType>();

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<VenueAddress> VenueAddresses => Set<VenueAddress>();

    public DbSet<Event> Events => Set<Event>();
    public DbSet<TicketRegistration> TicketRegistrations => Set<TicketRegistration>();

    // Membership (CAR-28, CAR-66). Login accounts are Identity's Users table (AppUser).
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Officer> Officers => Set<Officer>();
    public DbSet<OfficerHistory> OfficerHistory => Set<OfficerHistory>();

    /// <summary>
    /// Sets up the tables, relationships and rules. Check constraints are written in
    /// PostgreSQL syntax (double quotes around column names).
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity sets up its own tables first (AspNetUsers, AspNetRoles, claims, logins, tokens).
        base.OnModelCreating(modelBuilder);

        // PostgreSQL stores times with whole-second precision and a time zone. SQLite (local sample data and
        // tests) does not understand that type name, so it gets null and picks its own column type.
        var timestampColumnType = Database.IsNpgsql() ? "timestamp(0) with time zone" : null;

        // ---- Products ----
        // Merchandise and ticket types live in ONE table. A "ProductType" column says which kind
        // each row is: "Item" for merchandise, "Ticket" for a TicketType.
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasDiscriminator<string>("ProductType")
                .HasValue<Product>("Item")
                .HasValue<TicketType>("Ticket");
            entity.Property<string>("ProductType")
                .HasMaxLength(10)
                .HasDefaultValue("Item");

            // The database itself refuses bad data, even if the app has a bug.
            entity.ToTable("Products", table =>
            {
                table.HasCheckConstraint("CK_Products_Price", "\"Price\" >= 0");
                table.HasCheckConstraint("CK_Products_MemberPrice", "\"MemberPrice\" >= 0");
                table.HasCheckConstraint("CK_Products_StockQuantity", "\"StockQuantity\" >= 0");

                // A ticket must belong to an event, and merchandise must not.
                table.HasCheckConstraint(
                    "CK_Products_TicketEvent",
                    "(\"ProductType\" = 'Ticket' AND \"EventId\" IS NOT NULL) OR (\"ProductType\" = 'Item' AND \"EventId\" IS NULL)");
            });

            entity.HasKey(product => product.ProductId);
            entity.Property(product => product.Category).HasMaxLength(30).IsRequired();
            entity.Property(product => product.Name).HasMaxLength(100).IsRequired();
            entity.Property(product => product.Description).HasMaxLength(500);
            entity.Property(product => product.Price).HasPrecision(10, 2);
            entity.Property(product => product.MemberPrice).HasPrecision(10, 2);

            // Soft delete: deactivated products stay in the table but every normal query skips
            // them. Use IgnoreQueryFilters() to see them (officer screens do this).
            // HasSentinel(true) makes EF send IsActive = true instead of letting the database default decide.
            entity.Property(product => product.IsActive).HasDefaultValue(true).HasSentinel(true);
            entity.HasQueryFilter(product => product.IsActive);
        });

        // ---- Ticket types ----
        // A TicketType is a Product tied to one event. Restrict means an event can't be
        // deleted while ticket types still point at it.
        modelBuilder.Entity<TicketType>(entity =>
        {
            entity.HasOne(ticketType => ticketType.Event)
                .WithMany(@event => @event.TicketTypes)
                .HasForeignKey(ticketType => ticketType.EventId)
                .OnDelete(DeleteBehavior.Restrict);

            // Only index rows that have an event (ticket rows), since merchandise rows have none.
            entity.HasIndex(ticketType => ticketType.EventId)
                .HasDatabaseName("IX_Products_EventId")
                .HasFilter("\"EventId\" IS NOT NULL");
        });

        // ---- Orders ----
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders", table =>
            {
                table.HasCheckConstraint("CK_Orders_Status", "\"Status\" IN ('Pending', 'Fulfilled', 'Canceled')");
                table.HasCheckConstraint("CK_Orders_TotalAmount", "\"TotalAmount\" >= 0");
            });

            entity.HasKey(order => order.OrderId);
            entity.Property(order => order.Status).HasMaxLength(20).HasDefaultValue("Pending");
            entity.Property(order => order.OrderDate).HasDefaultValueSql("now()");
            entity.Property(order => order.TotalAmount).HasPrecision(10, 2);
        });

        // ---- Order lines ----
        // One row per product on an order. UnitPrice is copied onto the line when the order is
        // placed, so changing a product's price later never changes old orders.
        modelBuilder.Entity<OrderLine>(entity =>
        {
            entity.ToTable("OrderLines", table =>
            {
                table.HasCheckConstraint("CK_OrderLines_Quantity", "\"Quantity\" > 0");
                table.HasCheckConstraint("CK_OrderLines_UnitPrice", "\"UnitPrice\" >= 0");
            });

            entity.HasKey(line => line.OrderLineId);
            entity.Property(line => line.UnitPrice).HasPrecision(10, 2);
            entity.HasIndex(line => line.OrderId).HasDatabaseName("IX_OrderLines_OrderId");
            entity.HasIndex(line => line.ProductId).HasDatabaseName("IX_OrderLines_ProductId");

            // Neither an order nor a product can be deleted while order lines still use it.
            entity.HasOne(line => line.Order)
                .WithMany(order => order.OrderLines)
                .HasForeignKey(line => line.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Product>()
                .WithMany()
                .HasForeignKey(line => line.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Venues ----
        modelBuilder.Entity<Venue>(entity =>
        {
            entity.ToTable("Venues", table =>
                table.HasCheckConstraint("CK_Venues_Capacity", "\"Capacity\" > 0"));

            entity.HasKey(venue => venue.VenueId);
            entity.Property(venue => venue.Name).HasMaxLength(100).IsRequired();
            entity.Property(venue => venue.IsActive).HasDefaultValue(true).HasSentinel(true);

            // No two venues can share a name.
            entity.HasIndex(venue => venue.Name)
                .IsUnique()
                .HasDatabaseName("UX_Venues_Name");

            // Each venue has at most one address.
            entity.HasOne(venue => venue.Address)
                .WithOne(address => address.Venue)
                .HasForeignKey<VenueAddress>(address => address.VenueId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Venue addresses ----
        modelBuilder.Entity<VenueAddress>(entity =>
        {
            entity.ToTable("VenueAddresses");
            entity.HasKey(address => address.VenueAddressId);
            entity.Property(address => address.AddressLine1).HasMaxLength(100).IsRequired();
            entity.Property(address => address.AddressLine2).HasMaxLength(100);
            entity.Property(address => address.City).HasMaxLength(50).IsRequired();
            entity.Property(address => address.State).HasMaxLength(2).IsRequired();
            entity.Property(address => address.ZipCode).HasMaxLength(10).IsRequired();
        });

        // ---- Events ----
        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("Events", table =>
            {
                // An event must end after it starts, and can never sell more than its capacity.
                table.HasCheckConstraint("CK_Events_Times", "\"EndsAtUtc\" > \"StartsAtUtc\"");
                table.HasCheckConstraint("CK_Events_TicketCapacity", "\"TicketCapacity\" > 0");
                table.HasCheckConstraint(
                    "CK_Events_TicketsSold",
                    "\"TicketsSold\" >= 0 AND \"TicketsSold\" <= \"TicketCapacity\"");
            });

            entity.HasKey(@event => @event.EventId);
            entity.Property(@event => @event.Name).HasMaxLength(100).IsRequired();
            entity.Property(@event => @event.Description).HasMaxLength(500);

            // Times are stored as UTC. The converter marks them as UTC when they are read back.
            entity.Property(@event => @event.StartsAtUtc)
                .HasColumnType(timestampColumnType)
                .HasConversion<UtcDateTimeConverter>();
            entity.Property(@event => @event.EndsAtUtc)
                .HasColumnType(timestampColumnType)
                .HasConversion<UtcDateTimeConverter>();

            entity.Property(@event => @event.TicketsSold).HasDefaultValue(0);
            entity.Property(@event => @event.MembersOnly).HasDefaultValue(false);
            entity.Property(@event => @event.IsActive).HasDefaultValue(true).HasSentinel(true);

            // RowVersion is the concurrency token: if two people edit the same event, the second
            // save fails instead of silently overwriting the first.
            if (Database.IsNpgsql())
            {
                // PostgreSQL has no rowversion type. Every row already carries a hidden "xmin"
                // column that changes on each write, so we use it as the concurrency token.
                // EF reads it back after every save, and a stale save throws instead of overwriting.
                entity.Property(@event => @event.RowVersion)
                    .HasColumnName("xmin")
                    .HasColumnType("xid")
                    .ValueGeneratedOnAddOrUpdate()
                    .IsConcurrencyToken();
            }
            else
            {
                // SQLite (local sample data and tests) has no xmin, so the token is a plain column.
                entity.Property(@event => @event.RowVersion).IsConcurrencyToken();
            }

            entity.HasOne(@event => @event.Venue)
                .WithMany(venue => venue.Events)
                .HasForeignKey(@event => @event.VenueId)
                .OnDelete(DeleteBehavior.Restrict);

            // Speeds up the public event list (active events by start time). The INCLUDE columns
            // let the list be answered from the index alone without reading the full rows.
            entity.HasIndex(@event => @event.StartsAtUtc)
                .IncludeProperties(@event => new
                {
                    @event.EndsAtUtc,
                    @event.Name,
                    @event.VenueId,
                    @event.MembersOnly
                })
                .HasFilter("\"IsActive\" = true");

            // Speeds up "what is happening at this venue and when" checks.
            entity.HasIndex(@event => new { @event.VenueId, @event.StartsAtUtc })
                .IncludeProperties(@event => new { @event.EndsAtUtc, @event.IsActive });
        });

        // ---- Ticket registrations ----
        // One row per ticket sold. Canceling flips Status to Canceled instead of deleting the row.
        modelBuilder.Entity<TicketRegistration>(entity =>
        {
            entity.ToTable("TicketRegistrations", table =>
                table.HasCheckConstraint(
                    "CK_TicketRegistrations_Status",
                    "\"Status\" IN ('Purchased', 'Canceled')"));

            entity.HasKey(registration => registration.TicketRegistrationId);

            // The enum is saved as readable text ("Purchased"), not a number.
            entity.Property(registration => registration.Status)
                .HasConversion<string>()
                .HasMaxLength(10)
                .HasDefaultValue(RegistrationStatus.Purchased);
            entity.Property(registration => registration.CreatedAtUtc)
                .HasColumnType(timestampColumnType)
                .HasDefaultValueSql("now()")
                .HasConversion<UtcDateTimeConverter>();
            entity.Property(registration => registration.CanceledAtUtc)
                .HasColumnType(timestampColumnType)
                .HasConversion<UtcDateTimeConverter>();

            // Nothing a registration points at (event, order line, ticket type) can be deleted
            // while the registration exists.
            entity.HasOne(registration => registration.Event)
                .WithMany(@event => @event.Registrations)
                .HasForeignKey(registration => registration.EventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(registration => registration.OrderLine)
                .WithMany()
                .HasForeignKey(registration => registration.OrderLineId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TicketType>()
                .WithMany()
                .HasForeignKey(registration => registration.TicketTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(registration => new { registration.EventId, registration.Status });
            entity.HasIndex(registration => registration.OrderLineId);
            entity.HasIndex(registration => registration.TicketTypeId);
        });

        // ---- Membership (CAR-28 mapping notes, CAR-66) ----
        // Every relationship is Restrict (SQL NO ACTION): customers, members and officer history
        // are never deleted, so nothing may cascade into them. Status is never stored, and
        // members have no query filter, because most member screens need expired and inactive members too.
        // The "granted by" and "deactivated by" admin columns aren't linked yet; the Admins table comes in CAR-74.

        // ---- Customers ----
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(customer => customer.CustomerId);
            entity.Property(customer => customer.FirstName).HasMaxLength(50).IsRequired();
            entity.Property(customer => customer.LastName).HasMaxLength(50).IsRequired();
        });

        // ---- Login accounts (Identity's AspNetUsers table) ----
        // Every login belongs to exactly one customer, and a customer has at most one login.
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasOne(user => user.Customer)
                .WithOne()
                .HasForeignKey<AppUser>(user => user.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(user => user.CustomerId)
                .IsUnique()
                .HasDatabaseName("UX_AspNetUsers_CustomerId");
        });

        // ---- Members ----
        // MemberId is the CustomerId, so the database must not generate it. Dates are DateOnly,
        // which PostgreSQL stores as date (no time).
        modelBuilder.Entity<Member>(entity =>
        {
            entity.ToTable("Members");
            entity.HasKey(member => member.MemberId);
            entity.Property(member => member.MemberId).ValueGeneratedNever();
            entity.Property(member => member.IsActive).HasDefaultValue(true).HasSentinel(true);

            entity.HasOne(member => member.Customer)
                .WithOne(customer => customer.Member)
                .HasForeignKey<Member>(member => member.MemberId)
                .OnDelete(DeleteBehavior.Restrict);

            // Speeds up the roster queries (current members, by status).
            entity.HasIndex(member => new { member.IsActive, member.MemberExpiration })
                .HasDatabaseName("IX_Members_IsActive_MemberExpiration");
        });

        // ---- Officers ----
        // Holds only the current title. OfficerId is the MemberId, so it isn't generated either.
        modelBuilder.Entity<Officer>(entity =>
        {
            entity.ToTable("Officers");
            entity.HasKey(officer => officer.OfficerId);
            entity.Property(officer => officer.OfficerId).ValueGeneratedNever();
            entity.Property(officer => officer.Title).HasMaxLength(50).IsRequired();

            entity.HasOne(officer => officer.Member)
                .WithOne(member => member.Officer)
                .HasForeignKey<Officer>(officer => officer.OfficerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Officer history ----
        // Every title a member has held. At most one open row (no RevokedDate) per member.
        modelBuilder.Entity<OfficerHistory>(entity =>
        {
            entity.ToTable("OfficerHistory");
            entity.HasKey(history => history.OfficerHistoryId);
            entity.Property(history => history.Title).HasMaxLength(50).IsRequired();

            entity.HasOne(history => history.Member)
                .WithMany(member => member.OfficerHistory)
                .HasForeignKey(history => history.MemberId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(history => history.MemberId)
                .IsUnique()
                .HasFilter("\"RevokedDate\" IS NULL")
                .HasDatabaseName("UX_OfficerHistory_MemberId_Open");
        });

        // Identity's own relationships (claims, logins, tokens and roles pointing at a user)
        // default to cascade delete. We never delete accounts, so they follow the same
        // "block deletes" rule as every other table.
        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(entityType => entityType.GetForeignKeys()))
        {
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }
}
