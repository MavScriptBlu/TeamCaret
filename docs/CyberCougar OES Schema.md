# Cyber Cougars Club Order & Event System (CCOS)

Database Schema (SQL Server / Azure SQL)

---

This page used to hold hand-written `CREATE TABLE` scripts. They drifted from the design, so they've been replaced with pointers to where the schema is actually defined. **Don't build migrations from an old copy of the SQL in this file's history.**

## Where the schema is defined

| Source | What it answers |
| --- | --- |
| EF Core migrations in `backend/CCOS.Data/Migrations` | The exact tables, columns, constraints and indexes. This is the source of truth once the data project is merged |
| [ERD](./ERD_%20Cyber%20Cougars%20Club%20Order%20%26%20Event%20System%20(CCOS).md) | Every table, its columns and how tables relate |
| [Product Soft-Delete & Lifecycle Data Rules](./Soft_Delete_Lifecycle_Data_Rules.md) (CAR-24) | Product archiving, the Product query filter, order line snapshots |
| [Membership Lifecycle & Deactivation Schema](./Membership%20Lifecycle%20%26%20Deactivation%20Schema.md) (CAR-28) | Member status (Active/Expired/Inactive), officer history, member queries |
| [Event, Ticket & Venue Relational Schema](./Event%2C%20Ticket%20%26%20Venue%20Relational%20Schema.md) (CAR-30) | Events, venues, ticket types, ticket registrations, capacity and booking concurrency |
| [CRUD Lifecycle & Audit Policies](./lifecycleAuditSprint2.md) (CAR-31) | How every entity is created, deactivated and audited |

To see the full SQL the migrations produce, run this from `backend/`:

```
dotnet ef migrations script --idempotent --project CCOS.Data
```

## Rules that apply to every table

- **Nothing that history points to is ever deleted.** Products, venues, events and members are deactivated with `IsActive`; orders and ticket registrations change `Status`.
- **Foreign keys don't cascade into history.** Every foreign key into `Customers`, `Members`, `Admins`, `Products`, `Venues` and `Events` is `ON DELETE NO ACTION` (EF `DeleteBehavior.Restrict`). EF defaults required relationships to cascade, so each mapping states this explicitly.
- **Prices and names are snapshotted at checkout.** `OrderLines` keeps `UnitPrice` and `ProductNameSnapshot`, so old orders never change when a product is renamed, repriced or archived.
- **Times are stored in UTC** (`StartsAtUtc`, `CreatedAtUtc`, ...) and shown in club time (America/Chicago). Dates with no time, such as `MemberExpiration`, are compared against the club's local date.
- **Money** is `DECIMAL(10,2)` with a `CHECK (... >= 0)`.

## Quick reference by area

| Area | Tables | Key constraints | Details in |
| --- | --- | --- | --- |
| Products and orders | `Products`, `Orders`, `OrderLines` | `Products.IsActive` + global query filter; `ProductType` is `'Item'` or `'Ticket'`, and only tickets have an `EventId` | CAR-24, CAR-30 |
| Membership | `Customers`, `Members`, `Officers`, `OfficerHistory`, `Admins` | `MemberId` = `CustomerId`; status is worked out from `IsActive` + `MemberExpiration`, never stored; one open `OfficerHistory` row per member | CAR-28 |
| Events and tickets | `Venues`, `VenueAddresses`, `Events`, `TicketRegistrations` | `CHECK (TicketsSold <= TicketCapacity)`; booking raises `TicketsSold` with one conditional `UPDATE`; `EndsAtUtc > StartsAtUtc`; `Events.RowVersion` for officer edits | CAR-30 |
| Treasury | `TreasurySettings`, `Expenses`, `Deposits`, `Reconciliations` | Not designed yet | ERD only |
