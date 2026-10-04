using System.Data;
using CCOS.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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

    public override int SaveChanges() => SaveChanges(true);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PopulateProductNameSnapshotsAsync(false, CancellationToken.None).GetAwaiter().GetResult();
        var transaction = BeginSerializableEventValidationTransaction();
        try
        {
            ValidateEventVenueConstraintsAsync(false, CancellationToken.None).GetAwaiter().GetResult();
            var result = base.SaveChanges(acceptAllChangesOnSuccess);
            transaction?.Commit();
            return result;
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(true, cancellationToken);

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        await PopulateProductNameSnapshotsAsync(true, cancellationToken);
        var transaction = await BeginSerializableEventValidationTransactionAsync(cancellationToken);
        try
        {
            await ValidateEventVenueConstraintsAsync(true, cancellationToken);
            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return result;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
                table.HasCheckConstraint("CK_Products_Price", "[Price] >= 0");
                table.HasCheckConstraint("CK_Products_MemberPrice", "[MemberPrice] >= 0");
                table.HasCheckConstraint("CK_Products_StockQuantity", "[StockQuantity] >= 0");
                table.HasCheckConstraint(
                    "CK_Products_TicketEvent",
                    "(ProductType = 'Ticket' AND EventId IS NOT NULL) OR (ProductType = 'Item' AND EventId IS NULL)");
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
                .HasFilter("[EventId] IS NOT NULL");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders", table =>
            {
                table.HasCheckConstraint("CK_Orders_Status", "[Status] IN ('Pending', 'Fulfilled', 'Canceled')");
                table.HasCheckConstraint("CK_Orders_TotalAmount", "[TotalAmount] >= 0");
            });
            entity.HasKey(order => order.OrderId);
            entity.Property(order => order.Status).HasMaxLength(20).HasDefaultValue("Pending");
            entity.Property(order => order.OrderDate).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(order => order.TotalAmount).HasPrecision(10, 2);
        });

        modelBuilder.Entity<OrderLine>(entity =>
        {
            entity.ToTable("OrderLines", table =>
            {
                table.HasCheckConstraint("CK_OrderLines_Quantity", "[Quantity] > 0");
                table.HasCheckConstraint("CK_OrderLines_UnitPrice", "[UnitPrice] >= 0");
            });
            entity.HasKey(line => line.OrderLineId);
            entity.Property(line => line.UnitPrice).HasPrecision(10, 2);
            entity.Property(line => line.ProductNameSnapshot).HasMaxLength(100).IsRequired();
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
                table.HasCheckConstraint("CK_Venues_Capacity", "[Capacity] > 0"));
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
                table.HasCheckConstraint("CK_Events_Times", "[EndsAtUtc] > [StartsAtUtc]");
                table.HasCheckConstraint("CK_Events_TicketCapacity", "[TicketCapacity] > 0");
                table.HasCheckConstraint(
                    "CK_Events_TicketsSold",
                    "[TicketsSold] >= 0 AND [TicketsSold] <= [TicketCapacity]");
            });
            entity.HasKey(@event => @event.EventId);
            entity.Property(@event => @event.Name).HasMaxLength(100).IsRequired();
            entity.Property(@event => @event.Description).HasMaxLength(500);
            entity.Property(@event => @event.StartsAtUtc)
                .HasColumnType("datetime2(0)")
                .HasConversion<UtcDateTimeConverter>();
            entity.Property(@event => @event.EndsAtUtc)
                .HasColumnType("datetime2(0)")
                .HasConversion<UtcDateTimeConverter>();
            entity.Property(@event => @event.TicketsSold).HasDefaultValue(0);
            entity.Property(@event => @event.MembersOnly).HasDefaultValue(false);
            entity.Property(@event => @event.IsActive).HasDefaultValue(true).HasSentinel(true);
            if (Database.IsSqlServer())
            {
                entity.Property(@event => @event.RowVersion).IsRowVersion();
            }
            else
            {
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
                .HasFilter("[IsActive] = 1");
            entity.HasIndex(@event => new { @event.VenueId, @event.StartsAtUtc })
                .IncludeProperties(@event => new { @event.EndsAtUtc, @event.IsActive });
        });

        modelBuilder.Entity<TicketRegistration>(entity =>
        {
            entity.ToTable("TicketRegistrations", table =>
                table.HasCheckConstraint(
                    "CK_TicketRegistrations_Status",
                    "[Status] IN ('Purchased', 'Canceled')"));
            entity.HasKey(registration => registration.TicketRegistrationId);
            entity.Property(registration => registration.Status)
                .HasConversion<string>()
                .HasMaxLength(10)
                .HasDefaultValue(RegistrationStatus.Purchased);
            entity.Property(registration => registration.CreatedAtUtc)
                .HasColumnType("datetime2(0)")
                .HasDefaultValueSql("SYSUTCDATETIME()")
                .HasConversion<UtcDateTimeConverter>();
            entity.Property(registration => registration.CanceledAtUtc)
                .HasColumnType("datetime2(0)")
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

    private async Task PopulateProductNameSnapshotsAsync(bool useAsync, CancellationToken cancellationToken)
    {
        ChangeTracker.DetectChanges();
        var addedLines = ChangeTracker.Entries<OrderLine>()
            .Where(entry => entry.State == EntityState.Added)
            .ToList();
        var trackedProductNames = ChangeTracker.Entries<Product>()
            .GroupBy(entry => entry.Entity.ProductId)
            .ToDictionary(group => group.Key, group => group.First().Entity.Name);
        var productIdsToLoad = addedLines
            .Select(entry => entry.Entity.ProductId)
            .Where(productId => !trackedProductNames.ContainsKey(productId))
            .Distinct()
            .ToArray();
        var databaseProductNames = new Dictionary<int, string>();
        if (productIdsToLoad.Length > 0)
        {
            databaseProductNames = useAsync
                ? await Products.IgnoreQueryFilters()
                    .Where(product => productIdsToLoad.Contains(product.ProductId))
                    .ToDictionaryAsync(product => product.ProductId, product => product.Name, cancellationToken)
                : Products.IgnoreQueryFilters()
                    .Where(product => productIdsToLoad.Contains(product.ProductId))
                    .ToDictionary(product => product.ProductId, product => product.Name);
        }

        foreach (var lineEntry in addedLines)
        {
            var productId = lineEntry.Entity.ProductId;
            if (!trackedProductNames.TryGetValue(productId, out var productName)
                && !databaseProductNames.TryGetValue(productId, out productName))
            {
                throw new InvalidOperationException(
                    $"Product {productId} does not exist for the order line.");
            }

            lineEntry.Entity.ProductNameSnapshot = productName;
        }
    }

    private async Task ValidateEventVenueConstraintsAsync(bool useAsync, CancellationToken cancellationToken)
    {
        ChangeTracker.DetectChanges();
        var changedEventEntries = ChangeTracker.Entries<Event>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        var changedVenueEntries = ChangeTracker.Entries<Venue>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        if (changedEventEntries.Count == 0 && changedVenueEntries.Count == 0)
        {
            return;
        }

        var venueIds = changedEventEntries
            .Where(entry => entry.State != EntityState.Deleted)
            .Select(entry => entry.Entity.VenueId)
            .Concat(changedVenueEntries.Select(entry => entry.Entity.VenueId))
            .Distinct()
            .ToArray();

        var persistedVenues = useAsync
            ? await Venues.AsNoTracking().Where(venue => venueIds.Contains(venue.VenueId)).ToListAsync(cancellationToken)
            : Venues.AsNoTracking().Where(venue => venueIds.Contains(venue.VenueId)).ToList();
        var venueCapacities = persistedVenues.ToDictionary(venue => venue.VenueId, venue => venue.Capacity);
        foreach (var venueEntry in changedVenueEntries)
        {
            if (venueEntry.State == EntityState.Deleted || venueEntry.Entity.VenueId == 0)
            {
                venueCapacities.Remove(venueEntry.Entity.VenueId);
            }
            else
            {
                venueCapacities[venueEntry.Entity.VenueId] = venueEntry.Entity.Capacity;
            }
        }

        var persistedEvents = useAsync
            ? await Events.AsNoTracking().Where(@event => venueIds.Contains(@event.VenueId)).ToListAsync(cancellationToken)
            : Events.AsNoTracking().Where(@event => venueIds.Contains(@event.VenueId)).ToList();
        var finalEvents = persistedEvents.ToList();
        foreach (var eventEntry in changedEventEntries)
        {
            if (eventEntry.State != EntityState.Added)
            {
                finalEvents.RemoveAll(@event => @event.EventId == eventEntry.Entity.EventId);
            }

            if (eventEntry.State is EntityState.Added or EntityState.Modified)
            {
                eventEntry.Entity.StartsAtUtc = NormalizeUtc(eventEntry.Entity.StartsAtUtc);
                eventEntry.Entity.EndsAtUtc = NormalizeUtc(eventEntry.Entity.EndsAtUtc);
                finalEvents.Add(eventEntry.Entity);
            }
        }

        foreach (var eventEntry in changedEventEntries.Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            var @event = eventEntry.Entity;
            var venueCapacity = eventEntry.Reference(@event => @event.Venue).CurrentValue?.Capacity;
            if (venueCapacity is null)
            {
                if (!venueCapacities.TryGetValue(@event.VenueId, out var capacity))
                {
                    throw new InvalidOperationException(
                        $"Venue {@event.VenueId} does not exist for event {@event.EventId}.");
                }

                venueCapacity = capacity;
            }

            if (@event.TicketCapacity > venueCapacity)
            {
                throw new InvalidOperationException(
                    $"Event {@event.EventId} ticket capacity cannot exceed its venue capacity.");
            }

            if (@event.IsActive && finalEvents.Any(other =>
                    !ReferenceEquals(other, @event)
                    && other.IsActive
                    && EventsShareVenue(other, @event)
                    && other.StartsAtUtc < @event.EndsAtUtc
                    && other.EndsAtUtc > @event.StartsAtUtc))
            {
                throw new InvalidOperationException(
                    $"Active events at venue {@event.VenueId} cannot overlap.");
            }
        }

        var now = DateTime.UtcNow;
        foreach (var venueEntry in changedVenueEntries.Where(entry =>
                     entry.State == EntityState.Modified
                     && entry.Property(venue => venue.Capacity).OriginalValue > entry.Entity.Capacity))
        {
            var venueId = venueEntry.Entity.VenueId;
            if (finalEvents.Any(@event =>
                    @event.VenueId == venueId
                    && @event.IsActive
                    && @event.StartsAtUtc > now
                    && @event.TicketCapacity > venueEntry.Entity.Capacity))
            {
                throw new InvalidOperationException(
                    $"Venue {venueId} capacity cannot be reduced below an upcoming active event's ticket capacity.");
            }
        }
    }

    private IDbContextTransaction? BeginSerializableEventValidationTransaction()
    {
        if (!HasEventVenueChanges())
        {
            return null;
        }

        var currentTransaction = Database.CurrentTransaction;
        if (currentTransaction is not null)
        {
            EnsureSerializable(currentTransaction);
            return null;
        }

        return Database.BeginTransaction(IsolationLevel.Serializable);
    }

    private async Task<IDbContextTransaction?> BeginSerializableEventValidationTransactionAsync(
        CancellationToken cancellationToken)
    {
        if (!HasEventVenueChanges())
        {
            return null;
        }

        var currentTransaction = Database.CurrentTransaction;
        if (currentTransaction is not null)
        {
            EnsureSerializable(currentTransaction);
            return null;
        }

        return await Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    }

    private bool HasEventVenueChanges()
    {
        ChangeTracker.DetectChanges();
        return ChangeTracker.Entries<Event>()
                   .Any(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
               || ChangeTracker.Entries<Venue>()
                   .Any(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
    }

    private static void EnsureSerializable(IDbContextTransaction transaction)
    {
        if (transaction.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
        {
            throw new InvalidOperationException(
                "Event and venue changes require a serializable transaction.");
        }
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Local
            ? value.ToUniversalTime()
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static bool EventsShareVenue(Event first, Event second)
    {
        if (first.Venue is not null && ReferenceEquals(first.Venue, second.Venue))
        {
            return true;
        }

        return first.VenueId != 0
               && second.VenueId != 0
               && first.VenueId == second.VenueId;
    }
}
