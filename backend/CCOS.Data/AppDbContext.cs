using CCOS.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CCOS.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<TicketType> TicketTypes => Set<TicketType>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<VenueAddress> VenueAddresses => Set<VenueAddress>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<TicketRegistration> TicketRegistrations => Set<TicketRegistration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // PostgreSQL stores times with whole-second precision and a time zone. SQLite (local sample data and
        // tests) does not understand that type name, so it gets null and picks its own column type.
        var timestampColumnType = Database.IsNpgsql() ? "timestamp(0) with time zone" : null;

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasDiscriminator<string>("ProductType")
                .HasValue<Product>("Item")
                .HasValue<TicketType>("Ticket");
            entity.Property<string>("ProductType")
                .HasMaxLength(10)
                .HasDefaultValue("Item");
            entity.ToTable("Products", table =>
            {
                table.HasCheckConstraint("CK_Products_Price", "\"Price\" >= 0");
                table.HasCheckConstraint("CK_Products_MemberPrice", "\"MemberPrice\" >= 0");
                table.HasCheckConstraint("CK_Products_StockQuantity", "\"StockQuantity\" >= 0");
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
            entity.Property(product => product.IsActive).HasDefaultValue(true).HasSentinel(true);
            entity.HasQueryFilter(product => product.IsActive);
        });

        modelBuilder.Entity<TicketType>(entity =>
        {
            entity.HasOne(ticketType => ticketType.Event)
                .WithMany(@event => @event.TicketTypes)
                .HasForeignKey(ticketType => ticketType.EventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(ticketType => ticketType.EventId)
                .HasDatabaseName("IX_Products_EventId")
                .HasFilter("\"EventId\" IS NOT NULL");
        });

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
            entity.HasOne(line => line.Order)
                .WithMany(order => order.OrderLines)
                .HasForeignKey(line => line.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Product>()
                .WithMany()
                .HasForeignKey(line => line.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Venue>(entity =>
        {
            entity.ToTable("Venues", table =>
                table.HasCheckConstraint("CK_Venues_Capacity", "\"Capacity\" > 0"));
            entity.HasKey(venue => venue.VenueId);
            entity.Property(venue => venue.Name).HasMaxLength(100).IsRequired();
            entity.Property(venue => venue.IsActive).HasDefaultValue(true).HasSentinel(true);
            entity.HasIndex(venue => venue.Name)
                .IsUnique()
                .HasDatabaseName("UX_Venues_Name");
            entity.HasOne(venue => venue.Address)
                .WithOne(address => address.Venue)
                .HasForeignKey<VenueAddress>(address => address.VenueId)
                .OnDelete(DeleteBehavior.Restrict);
        });

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

        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("Events", table =>
            {
                table.HasCheckConstraint("CK_Events_Times", "\"EndsAtUtc\" > \"StartsAtUtc\"");
                table.HasCheckConstraint("CK_Events_TicketCapacity", "\"TicketCapacity\" > 0");
                table.HasCheckConstraint(
                    "CK_Events_TicketsSold",
                    "\"TicketsSold\" >= 0 AND \"TicketsSold\" <= \"TicketCapacity\"");
            });
            entity.HasKey(@event => @event.EventId);
            entity.Property(@event => @event.Name).HasMaxLength(100).IsRequired();
            entity.Property(@event => @event.Description).HasMaxLength(500);
            entity.Property(@event => @event.StartsAtUtc)
                .HasColumnType(timestampColumnType)
                .HasConversion<UtcDateTimeConverter>();
            entity.Property(@event => @event.EndsAtUtc)
                .HasColumnType(timestampColumnType)
                .HasConversion<UtcDateTimeConverter>();
            entity.Property(@event => @event.TicketsSold).HasDefaultValue(0);
            entity.Property(@event => @event.MembersOnly).HasDefaultValue(false);
            entity.Property(@event => @event.IsActive).HasDefaultValue(true).HasSentinel(true);
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
            entity.HasIndex(@event => @event.StartsAtUtc)
                .IncludeProperties(@event => new
                {
                    @event.EndsAtUtc,
                    @event.Name,
                    @event.VenueId,
                    @event.MembersOnly
                })
                .HasFilter("\"IsActive\" = true");
            entity.HasIndex(@event => new { @event.VenueId, @event.StartsAtUtc })
                .IncludeProperties(@event => new { @event.EndsAtUtc, @event.IsActive });
        });

        modelBuilder.Entity<TicketRegistration>(entity =>
        {
            entity.ToTable("TicketRegistrations", table =>
                table.HasCheckConstraint(
                    "CK_TicketRegistrations_Status",
                    "\"Status\" IN ('Purchased', 'Canceled')"));
            entity.HasKey(registration => registration.TicketRegistrationId);
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
    }
}
