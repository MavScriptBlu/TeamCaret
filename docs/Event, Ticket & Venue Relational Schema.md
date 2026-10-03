CAR-30: Event, Ticket & Venue Relational Schema
Oct 2, 2026 · @Dylan
Story: As a club officer, I want relational database model specifications for Events, Venues, and Tickets, so that ticket sales cannot exceed venue capacity and event schedules are stored reliably.
In short: a Venue hosts Events. Each Event has one or more TicketTypes (for example "General" and "Member"), which are sold through normal orders. Each admission sold becomes one TicketRegistration row. The Event keeps a running count of tickets sold, and SQL Server itself refuses any sale that would push that count past the event's capacity. Capacity can never be set higher than the venue holds.
Acceptance criterion
Covered in
Map entities for Event, Venue, TicketType, and TicketRegistration with required fields and constraints
Section 1
Define capacity validation constraints and concurrency check rules for ticket booking
Section 2
Specify indexes for date-range queries and venue lookups
Section 3
Document DbContext relationship mappings and navigation properties
Section 4
Based on: the current ERD on Dev, where tickets are Products linked to an Event (EVENT ||--o{ PRODUCT : "has ticket products") and are bought through Orders and OrderLines alongside swag. The first Product migration leaves EventId out, so the Event migration adds it.
#
Rule
Schema change?
R1
A TicketType is a Product with ProductType = 'Ticket' and an EventId. It's a subtype of Product in EF, stored in the same Products table
Yes: adds ProductType; EventId as in the ERD
R2
A TicketRegistration is one admission to one event, created at checkout from an order line
Yes: new table
R3
Events.TicketsSold counts active registrations; a CHECK keeps it between 0 and TicketCapacity
Yes: new column
R4
Booking raises TicketsSold with one conditional UPDATE. If no row changes, the event is sold out and the whole order rolls back
No (booking rule)
R5
An event's TicketCapacity can't exceed its venue's Capacity, or drop below TicketsSold
No (validation rule)
R6
Events stores a start and an end time in UTC (StartsAtUtc, EndsAtUtc), with a CHECK that the end is after the start
Yes: replaces EventDateTime
R7
Venues, events, ticket types and registrations are never deleted. Venues and events use IsActive, and registrations use Status = 'Canceled'
Yes: adds IsActive to Venues and Events
R8
Every foreign key in this area is ON DELETE NO ACTION
No
1. Entities, fields and constraints
Four entities cover this story. Venue and Event are their own tables. TicketType lives in Products so tickets can be sold through the same orders as swag. TicketRegistration is a new table with one row per admission.
The dashed line is a count, not a key: TicketsSold on the event goes up and down as registrations are created and canceled.
Venues
Column
Type
Constraint
VenueId
INT IDENTITY
PK
Name
NVARCHAR(100) NOT NULL
UNIQUE
Capacity
INT NOT NULL
CHECK (Capacity > 0)
IsActive
BIT NOT NULL DEFAULT 1
Retired venues can't host new events; past events keep pointing at them
The venue's address stays in VenueAddresses (one row per venue), as in the ERD.
Events
Column
Type
Constraint
EventId
INT IDENTITY
PK
VenueId
INT NOT NULL
FK → Venues, NO ACTION
Name
NVARCHAR(100) NOT NULL

Description
NVARCHAR(500) NULL

StartsAtUtc
DATETIME2(0) NOT NULL
Stored in UTC, shown in club time (America/Chicago)
EndsAtUtc
DATETIME2(0) NOT NULL
CHECK (EndsAtUtc > StartsAtUtc)
TicketCapacity
INT NOT NULL
CHECK (TicketCapacity > 0); never more than the venue's Capacity (Section 2)
TicketsSold
INT NOT NULL DEFAULT 0
CHECK (TicketsSold >= 0 AND TicketsSold <= TicketCapacity)
MembersOnly
BIT NOT NULL DEFAULT 0
As in the ERD
IsActive
BIT NOT NULL DEFAULT 1
0 = canceled or unpublished; hidden from the public event list
RowVersion
ROWVERSION
Concurrency token for officer edits (Section 2)
The ERD's single EventDateTime becomes StartsAtUtc plus EndsAtUtc. With an end time, the app can show how long an event runs and check for double-booked venues. Storing UTC means the times stay correct across daylight saving changes.
TicketType (rows in Products)
Column
Type
Constraint
ProductType
NVARCHAR(10) NOT NULL DEFAULT 'Item'
CHECK (ProductType IN ('Item', 'Ticket'))
EventId
INT NULL
FK → Events, NO ACTION. CHECK ((ProductType = 'Ticket' AND EventId IS NOT NULL) OR (ProductType = 'Item' AND EventId IS NULL))
Name, Price, MemberPrice, IsActive
(existing Product columns)
A ticket type uses them as-is, for example "General" at $5 with a $0 member price
StockQuantity
(existing)
Always 0 for tickets; ticket availability comes from the event's capacity, not stock
Archiving a ticket type uses the existing Product soft delete, so it stops selling but stays on past orders.
TicketRegistrations
Column
Type
Constraint
TicketRegistrationId
INT IDENTITY
PK
EventId
INT NOT NULL
FK → Events, NO ACTION. Copied from the ticket type at checkout
TicketTypeId
INT NOT NULL
FK → Products, NO ACTION
OrderLineId
INT NOT NULL
FK → OrderLines, NO ACTION
Status
NVARCHAR(10) NOT NULL DEFAULT 'Purchased'
CHECK (Status IN ('Purchased', 'Canceled'))
CreatedAtUtc
DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME()

CanceledAtUtc
DATETIME2(0) NULL
Set when the order is canceled
An order line for 3 tickets creates 3 registrations. The price paid stays on the order line (UnitPrice), so it isn't repeated here.
2. Capacity validation and concurrency
Overselling is blocked in the database, not just in the app. TicketsSold can only go up through one conditional UPDATE that also checks capacity, and a CHECK constraint refuses any value over TicketCapacity, even from code that forgets the condition.
Capacity rules
Rule
Enforced by
When it's checked
TicketsSold <= TicketCapacity
CHECK on Events
Every write, always
TicketsSold >= 0
CHECK on Events
Every write, always
TicketCapacity <= Venues.Capacity
App validation (SQL Server can't CHECK across tables)
Creating or editing an event, moving it to another venue, or lowering a venue's capacity
TicketCapacity >= TicketsSold
The same CHECK as the first row
An officer lowering capacity below what's sold gets an error
No double-booked venue
App validation
Saving an event: no other active event at the venue overlaps StartsAtUtc–EndsAtUtc
Lowering a venue's capacity is refused while any upcoming active event there has a higher TicketCapacity.
Booking (inside the order's transaction)
1. Check the event is active and hasn't started, the ticket type is active, and, for a members-only event, the buyer is a current member.
2. Reserve the seats with one statement per event:
var reserved = await db.Events
    .Where(e => e.EventId == eventId
             && e.IsActive
             && e.TicketsSold + quantity <= e.TicketCapacity)
    .ExecuteUpdateAsync(s => s.SetProperty(e => e.TicketsSold, e => e.TicketsSold + quantity));

if (reserved == 0)   // sold out, or not enough seats left
    throw new SoldOutException(eventId);   // whole order rolls back
3. Insert the order line, then one TicketRegistrations row per admission with Status = 'Purchased'.
4. Commit. If anything fails, the rollback also undoes the seat reservation.
Why two buyers can't take the last seat: SQL Server locks the event row while the UPDATE runs. The second buyer's UPDATE waits, then checks TicketsSold + quantity <= TicketCapacity against the new count, matches no row, and returns 0. This works at the default isolation level. Azure SQL's default read-committed snapshot doesn't change it, because an UPDATE always checks the latest committed row.
Orders with tickets to more than one event reserve seats in EventId order. Two orders then always lock events in the same order and can't deadlock each other.
Canceling an order runs in one transaction: set its registrations to Status = 'Canceled' with CanceledAtUtc, then lower TicketsSold by the same number with one ExecuteUpdate. The seats go straight back on sale.
Officer edits use optimistic concurrency. Events.RowVersion is EF's concurrency token. Every save of an event, and every booking or cancellation, changes it. If an officer saves an edit after a ticket sold, or after another officer saved, EF throws DbUpdateConcurrencyException. The edit screen then reloads the event and asks them to review and save again. No one silently overwrites a newer capacity or schedule.
Consistency check (for tests and the admin dashboard): TicketsSold should always equal the number of Purchased registrations for that event:
SELECT e.EventId, e.TicketsSold, COUNT(r.TicketRegistrationId) AS Purchased
FROM Events e
LEFT JOIN TicketRegistrations r ON r.EventId = e.EventId AND r.Status = 'Purchased'
GROUP BY e.EventId, e.TicketsSold
HAVING e.TicketsSold <> COUNT(r.TicketRegistrationId);   -- should return no rows
3. Indexes
Two indexes on Events cover the date-range and venue queries. The rest support foreign keys and the ticket counts.
Index
Columns
Used by
IX_Events_StartsAtUtc
(StartsAtUtc) INCLUDE (EndsAtUtc, Name, VenueId, MembersOnly) WHERE IsActive = 1
Upcoming events and "events this month" on the public list
IX_Events_VenueId_StartsAtUtc
(VenueId, StartsAtUtc) INCLUDE (EndsAtUtc, IsActive)
A venue's schedule, the double-booking check, and the VenueId foreign key
UX_Venues_Name
(Name) unique
Venue lookup by name; stops duplicate venues
IX_Products_EventId
(EventId) WHERE EventId IS NOT NULL
The ticket types for an event
IX_TicketRegistrations_EventId_Status
(EventId, Status)
Attendee lists and the TicketsSold consistency check
IX_TicketRegistrations_OrderLineId
(OrderLineId)
Canceling an order's registrations
IX_TicketRegistrations_TicketTypeId
(TicketTypeId)
The TicketTypeId foreign key
IX_Events_VenueId_StartsAtUtc replaces the schema doc's IX_Events_VenueId, since its first column already serves venue lookups.
Query shapes these indexes expect
Query
Condition
Events in a date range
StartsAtUtc >= @fromUtc AND StartsAtUtc < @toUtc
Upcoming events
IsActive = 1 AND StartsAtUtc >= @nowUtc, ordered by StartsAtUtc
A venue's schedule
VenueId = @venueId AND StartsAtUtc >= @fromUtc
Overlap at a venue
VenueId = @venueId AND IsActive = 1 AND StartsAtUtc < @newEndUtc AND EndsAtUtc > @newStartUtc
Ranges are half-open (>= start, < end), so an event at exactly midnight falls in one day, not two. The app turns club-time bounds, such as "October 2026 in America/Chicago", into UTC before querying. The query must compare the raw column: wrapping it in a function, such as CAST(StartsAtUtc AS date), stops SQL Server from using the index.
4. DbContext mappings and navigation properties
Every relationship is DeleteBehavior.Restrict (SQL NO ACTION). TicketType is mapped as a subtype of Product using EF's table-per-hierarchy, with ProductType as the discriminator column.
Entity
Navigation properties
Notes
Venue
Address (one VenueAddress), Events (many)

Event
Venue (required), TicketTypes (many), Registrations (many)
RowVersion concurrency token; no global query filter
TicketType : Product
Event (required)
Adds EventId; inherits name, prices and IsActive from Product
TicketRegistration
Event (required), OrderLine (required)
Keeps TicketTypeId as a plain foreign key, with no TicketType navigation
Why TicketRegistration has no TicketType navigation: Product has the IsActive global query filter from CAR-24, and TicketType inherits it. A required navigation to a filtered entity makes EF silently drop the registrations of archived ticket types from query results, the same problem CAR-24 designed out for order lines. Attendee lists join to the ticket type by TicketTypeId and bypass the filter on purpose.
Why Event has no global query filter: registrations and ticket types must always reach their event, even a canceled one. Public event queries add IsActive themselves (Section 3).
// Product / TicketType: one table, told apart by ProductType
modelBuilder.Entity<Product>(b =>
{
    b.HasDiscriminator<string>("ProductType")
     .HasValue<Product>("Item")
     .HasValue<TicketType>("Ticket");
    b.Property("ProductType").HasMaxLength(10);
    b.ToTable("Products", t => t.HasCheckConstraint("CK_Products_TicketEvent",
        "(ProductType = 'Ticket' AND EventId IS NOT NULL) OR (ProductType = 'Item' AND EventId IS NULL)"));
});

modelBuilder.Entity<TicketType>(b =>
{
    b.HasOne(t => t.Event).WithMany(e => e.TicketTypes)
     .HasForeignKey(t => t.EventId)
     .OnDelete(DeleteBehavior.Restrict);
    b.HasIndex(t => t.EventId).HasFilter("[EventId] IS NOT NULL");
});

modelBuilder.Entity<Event>(b =>
{
    b.ToTable("Events", t =>
    {
        t.HasCheckConstraint("CK_Events_Times", "[EndsAtUtc] > [StartsAtUtc]");
        t.HasCheckConstraint("CK_Events_TicketCapacity", "[TicketCapacity] > 0");
        t.HasCheckConstraint("CK_Events_TicketsSold", "[TicketsSold] >= 0 AND [TicketsSold] <= [TicketCapacity]");
    });
    b.Property(e => e.Name).HasMaxLength(100);
    b.Property(e => e.Description).HasMaxLength(500);
    b.Property(e => e.StartsAtUtc).HasPrecision(0);
    b.Property(e => e.EndsAtUtc).HasPrecision(0);
    b.Property(e => e.TicketsSold).HasDefaultValue(0);
    b.Property(e => e.IsActive).HasDefaultValue(true).HasSentinel(true);
    b.Property(e => e.RowVersion).IsRowVersion();

    b.HasOne(e => e.Venue).WithMany(v => v.Events)
     .HasForeignKey(e => e.VenueId)
     .OnDelete(DeleteBehavior.Restrict);

    b.HasIndex(e => e.StartsAtUtc)
     .IncludeProperties(e => new { e.EndsAtUtc, e.Name, e.VenueId, e.MembersOnly })
     .HasFilter("[IsActive] = 1");
    b.HasIndex(e => new { e.VenueId, e.StartsAtUtc })
     .IncludeProperties(e => new { e.EndsAtUtc, e.IsActive });
});

modelBuilder.Entity<Venue>(b =>
{
    b.ToTable("Venues", t => t.HasCheckConstraint("CK_Venues_Capacity", "[Capacity] > 0"));
    b.Property(v => v.Name).HasMaxLength(100);
    b.HasIndex(v => v.Name).IsUnique();
    b.Property(v => v.IsActive).HasDefaultValue(true).HasSentinel(true);
    b.HasOne(v => v.Address).WithOne(a => a.Venue)
     .HasForeignKey<VenueAddress>(a => a.VenueId)
     .OnDelete(DeleteBehavior.Restrict);
});

modelBuilder.Entity<TicketRegistration>(b =>
{
    b.Property(r => r.Status).HasConversion<string>().HasMaxLength(10)
     .HasDefaultValue(RegistrationStatus.Purchased);
    b.ToTable("TicketRegistrations", t => t.HasCheckConstraint("CK_TicketRegistrations_Status",
        "[Status] IN ('Purchased', 'Canceled')"));

    b.HasOne(r => r.Event).WithMany(e => e.Registrations)
     .HasForeignKey(r => r.EventId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne(r => r.OrderLine).WithMany()
     .HasForeignKey(r => r.OrderLineId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne<TicketType>().WithMany()
     .HasForeignKey(r => r.TicketTypeId).OnDelete(DeleteBehavior.Restrict);

    b.HasIndex(r => new { r.EventId, r.Status });
    b.HasIndex(r => r.OrderLineId);
});
RegistrationStatus is a C# enum (Purchased, Canceled) stored as text, matching the CHECK.
Other mapping notes
• EF reads DateTime values back with Kind = Unspecified. Add a value converter on StartsAtUtc, EndsAtUtc, CreatedAtUtc and CanceledAtUtc that marks them as UTC, so converting to club time is always correct.
• db.Products returns swag and ticket types together. The swag catalog filters with .Where(p => !(p is TicketType)); ticket types are read through db.Set<TicketType>().
Mapping checks after the migration is generated:
• No foreign key in this area shows Cascade.
• Products has a ProductType column, EventId is nullable, and CK_Products_TicketEvent exists.
• Events has TicketsSold with default 0, RowVersion as rowversion, and the three CHECK constraints.
• Both Events indexes include their INCLUDE columns, and the first has the IsActive = 1 filter.
Decisions
Question
Decision
Reason
Separate TicketTypes table, or tickets as Products?
Products, as an EF subtype (ProductType = 'Ticket')
Keeps the ERD's design: tickets and swag share one checkout, one OrderLines table and one soft delete
Count sold tickets, or keep a counter?
Counter (TicketsSold) with a CHECK, kept in step with registrations
A conditional UPDATE on one row blocks overselling in the database; counting rows would need stricter locking
Database check for event capacity vs. venue capacity?
No, app validation
SQL Server CHECK constraints can't compare two tables, and a trigger is harder to maintain than one validation method
Per-ticket-type limits (for example, 20 member tickets)?
Not now; one capacity per event
The story is about venue capacity; type limits would need a second counter
One start time, or start and end?
Start and end, in UTC
Needed for event length and the double-booked venue check; UTC survives daylight saving changes
Reserved ticket status?
No: only Purchased and Canceled
Checkout is one transaction, so there is no hold step to represent
Global query filter on Event?
No
Registrations and ticket types must always reach their event, even a canceled one
Delete events, venues or registrations?
Never; IsActive and Status instead
Past orders and attendee history must keep pointing at real rows