CAR-28: Membership Lifecycle & Deactivation Schema
Issue: CAR-28 · Author: Dylan Morzfeld · Last updated: 2026-10-01

Story: As a system administrator, I want data schema rules for member status transitions (Active, Inactive, Expired), so that historical ticket purchases and officer history remain intact when a member leaves the club.

In short: member rows are never deleted. A member is Active, Expired (the expiration date has passed) or Inactive (an admin marked them as having left the club). Expired is worked out from the date and is never set by hand. Leaving the club changes two columns on the Members row and nothing else, so the Customer row, their ticket purchases and their officer history all stay.

Acceptance criterion
Covered in
Define MemberStatus enum/lookup and expiration date tracking attributes
Section 1
Document foreign key behavior for Customer and Member records when status changes
Section 2
Define queries for active roster filtering and historical member lookup
Section 3
Compile DbContext mapping notes in team documentation
Section 4


Based on: the current ERD on Dev (Member, Officer, OfficerHistory, Admin, Email; event tickets are Products with an EventId). The .NET projects target .NET 10, so EF Core 10.

#
Rule
Schema change?
R1
MemberStatus is a C# enum with three values: Active, Inactive, Expired
No (code only)
R2
Status is never stored. It's worked out from two columns: IsActive (set by an admin) and MemberExpiration (a date)
Yes: replaces the ERD's IsCurrent
R3
Members adds IsActive, JoinedDate, DeactivatedDate, DeactivatedByAdminId
Yes
R4
Member rows are never deleted. Leaving the club sets IsActive = 0; rejoining reuses the same row
No
R5
Every foreign key into Customers, Members and Admins is ON DELETE NO ACTION
Yes: two CASCADE rules change
R6
Only an Active member gets member pricing, members-only access and officer access
No
R7
Deactivating a member who holds an officer title also closes that title, in the same transaction
No

1. MemberStatus and expiration tracking
A member's status comes from two stored columns. IsActive says whether they're still in the club, and MemberExpiration says how long their dues last. Status itself is never stored, so nothing needs a nightly job and nothing can go stale.

// CCOS.Shared/MemberStatus.cs (shared so the web app can display it)

public enum MemberStatus { Active, Inactive, Expired }

Status
Rule
Member pricing + members-only
Officer access
Active
IsActive = 1 and MemberExpiration is today or later
Yes
Yes, if they hold a title
Expired
IsActive = 1 and MemberExpiration is before today
No
Paused until renewed
Inactive
IsActive = 0 (any date)
No
No; their title is closed


Inactive is checked first, so someone who left the club shows as Inactive even if their dues date is still in the future. "Today" is the club's local date (America/Chicago), not the server's UTC date, so dues don't lapse at 7 p.m. the evening before.

Tracking columns on Members

Column
Type
Rule
MemberId
INT PK, FK → Customers
Same value as the CustomerId; already in the ERD
MemberExpiration
DATE NOT NULL
Last day dues cover. Renewing overwrites it; already in the ERD
IsActive
BIT NOT NULL DEFAULT 1
Set to 0 only when an admin marks the member as having left
JoinedDate
DATE NOT NULL, set by the app
First time they became a member; never changes, even after rejoining
DeactivatedDate
DATE NULL
When they were marked as left; cleared on rejoin
DeactivatedByAdminId
INT NULL FK → Admins
Which admin marked them as left; cleared on rejoin


The ERD's IsCurrent column is dropped. A stored flag goes stale the day dues run out. The schema doc's PERSISTED computed version can't be created either, because SQL Server doesn't allow GETDATE() in a persisted column.

stateDiagram-v2

    [*] --> Active: admin adds member

    Active --> Expired: date passes (automatic)

    Expired --> Active: admin renews (new date)

    Active --> Inactive: admin - left the club

    Expired --> Inactive: admin - left the club

    Inactive --> Active: admin re-adds (same row)

No path leads to deletion: the Members row, its Customer and its history always stay.

Change
Columns written
Who
Add a member
New Members row: MemberExpiration, IsActive = 1, JoinedDate
Admin
Dues run out
Nothing; status reads as Expired from the date
Automatic
Renew
MemberExpiration = new date
Admin
Mark as left
IsActive = 0, DeactivatedDate, DeactivatedByAdminId
Admin
Rejoin
IsActive = 1, clear DeactivatedDate and DeactivatedByAdminId, new MemberExpiration
Admin


Rejoining always reuses the same row, because MemberId is the person's CustomerId. There can never be a duplicate member.
2. Foreign key behavior when status changes
A status change is an UPDATE to two or three columns on Members, so no foreign key fires and no related row changes. Foreign keys only act on deletes, and CCOS never deletes customers, members or admins. Every key into those tables is therefore NO ACTION, so the database itself refuses a delete that would break history.

Relationship
SQL ON DELETE
Change?
What happens when the member leaves or expires
Members → Customers (MemberId)
NO ACTION
Yes, was CASCADE
Nothing. The Customer row, its emails, phones and addresses stay
Orders → Customers
NO ACTION
No
Nothing. Every order, including event ticket purchases, stays
Officers → Members (OfficerId)
NO ACTION
Yes, was CASCADE
Only on leaving: the current title is closed (below)
OfficerHistory → Members
NO ACTION
New in ERD
Nothing. Every past title stays, with its dates
Officers / OfficerHistory → Admins (GrantedByAdminId)
NO ACTION
New in ERD
Nothing
Members → Admins (DeactivatedByAdminId)
NO ACTION
New column
Records who marked them as left


Ticket purchases are safe by design. Orders belong to the Customer, not the Member, so membership changes can't reach them. The member price a ticket was bought at is kept in OrderLines.UnitPrice, so an expired or inactive member's old orders still show what they actually paid.

Officer history is safe by design. OfficerHistory points at MemberId, and member rows are never deleted. When an Active officer is marked as left, one transaction does three things:

Sets IsActive = 0, DeactivatedDate and DeactivatedByAdminId on Members.
Sets RevokedDate on their open OfficerHistory row.
Deletes their Officers row, which only holds the current title.

When an officer's dues simply expire, nothing is written. Their officer access pauses because the access check requires an Active member, and it comes back when they renew.

Why the two CASCADE rules change: with CASCADE, deleting one Customer row (for example from a cleanup script) would silently delete their membership and current title too. With NO ACTION, SQL Server refuses that delete instead. EF Core uses cascade by default for required relationships, so the mapping must say Restrict explicitly (Section 4).
3. Queries
Every member query takes today (the club's local date) as a parameter instead of reading the clock inside the query. This keeps all queries on the same definition of "today" and lets tests pass any date they want.

Unlike Products, Member gets no global query filter. A member's status changes with the calendar, and most member screens (renewals, history, the roster's "show expired" view) need non-active members. Each query below says exactly which members it wants.

Active roster (members in good standing, with their primary email):

db.Members

  .Where(m => m.IsActive && m.MemberExpiration >= today)

  .OrderBy(m => m.Customer.LastName).ThenBy(m => m.Customer.FirstName)

  .Select(m => new RosterRow(

      m.MemberId,

      m.Customer.FirstName + " " + m.Customer.LastName,

      m.Customer.Emails.Where(e => e.IsPrimary).Select(e => e.EmailAddress).FirstOrDefault(),

      m.MemberExpiration))

SQL equivalent: WHERE m.IsActive = 1 AND m.MemberExpiration >= @today.

Roster by status (the admin's filter: Active, Expired, Inactive or all). The status is worked out inside the query, so it can be sorted and filtered on:

db.Members.Select(m => new {

    Member = m,

    Status = !m.IsActive ? MemberStatus.Inactive

           : m.MemberExpiration < today ? MemberStatus.Expired

           : MemberStatus.Active })

  .Where(x => statusFilter == null || x.Status == statusFilter)

EF turns the status expression into a SQL CASE, so the filtering happens in the database.

Is this customer a current member? (used for member pricing and members-only checks):

db.Members.Any(m => m.MemberId == customerId && m.IsActive && m.MemberExpiration >= today)

No row, an inactive row or an expired row all mean "not a member right now".

Historical member lookup (one person, whatever their status):

db.Members

  .Where(m => m.MemberId == memberId)

  .Select(m => new {

      m.Customer.FirstName, m.Customer.LastName,

      m.JoinedDate, m.MemberExpiration, m.IsActive,

      m.DeactivatedDate, m.DeactivatedByAdminId,

      Titles = m.OfficerHistory

          .OrderByDescending(h => h.GrantedDate)

          .Select(h => new { h.Title, h.GrantedDate, h.RevokedDate }) })

This finds anyone who was ever a member, including people who left years ago, along with every officer title they held. Their purchases come from the existing order history query by CustomerId, which works whatever their membership status is.

Who held a title on a given date (for example, "who was Treasurer last spring?"):

db.OfficerHistory.Where(h => h.Title == title

    && h.GrantedDate <= onDate && (h.RevokedDate == null || h.RevokedDate >= onDate))

One limit to know: renewing overwrites MemberExpiration, so the schema records when someone first joined and when their dues currently run out, not every past dues period. If the club ever needs "was this person a member on March 1 last year?", that needs a dues-payment history table, which is outside this story.
4. DbContext mapping notes
The membership tables map with plain Fluent API configuration. Every relationship is Restrict (SQL NO ACTION), MemberStatus is not a column, and there is no query filter.

Entity
Key
Relationships (all DeleteBehavior.Restrict)
Notes
Customer
CustomerId (identity)
one Member (optional), many Emails, many Orders
Unchanged by membership status
Member
MemberId, not generated
one Customer (MemberId = CustomerId), one Officer (optional), many OfficerHistory, DeactivatedByAdminId → Admin
IsActive default true; dates are DateOnly
Officer
OfficerId, not generated
one Member (OfficerId = MemberId), GrantedByAdminId → Admin
Holds the current title only
OfficerHistory
OfficerHistoryId (identity)
MemberId → Member, GrantedByAdminId → Admin
At most one open row (RevokedDate IS NULL) per member
Admin
AdminId (identity)
referenced by the three tables above
Never deleted; has its own IsActive


modelBuilder.Entity<Member>(b =>

{

    b.HasKey(m => m.MemberId);

    b.Property(m => m.MemberId).ValueGeneratedNever();   // it's the CustomerId

    b.Property(m => m.IsActive).HasDefaultValue(true).HasSentinel(true);

    b.HasOne(m => m.Customer).WithOne(c => c.Member)

     .HasForeignKey<Member>(m => m.MemberId)

     .OnDelete(DeleteBehavior.Restrict);

    b.HasOne<Admin>().WithMany()

     .HasForeignKey(m => m.DeactivatedByAdminId)

     .OnDelete(DeleteBehavior.Restrict);

    b.HasIndex(m => new { m.IsActive, m.MemberExpiration });   // roster queries

});

modelBuilder.Entity<Officer>(b =>

{

    b.HasKey(o => o.OfficerId);

    b.Property(o => o.OfficerId).ValueGeneratedNever();   // it's the MemberId

    b.HasOne(o => o.Member).WithOne(m => m.Officer)

     .HasForeignKey<Officer>(o => o.OfficerId)

     .OnDelete(DeleteBehavior.Restrict);

    b.HasOne<Admin>().WithMany().HasForeignKey(o => o.GrantedByAdminId)

     .OnDelete(DeleteBehavior.Restrict);

});

modelBuilder.Entity<OfficerHistory>(b =>

{

    b.HasOne(h => h.Member).WithMany(m => m.OfficerHistory)

     .HasForeignKey(h => h.MemberId)

     .OnDelete(DeleteBehavior.Restrict);

    b.HasOne<Admin>().WithMany().HasForeignKey(h => h.GrantedByAdminId)

     .OnDelete(DeleteBehavior.Restrict);

    b.HasIndex(h => h.MemberId).IsUnique().HasFilter("[RevokedDate] IS NULL");

});

Status on a loaded member: Member gets a method rather than a stored property, so it always uses the date it's given:

public MemberStatus StatusOn(DateOnly today) =>

    !IsActive ? MemberStatus.Inactive

    : MemberExpiration < today ? MemberStatus.Expired

    : MemberStatus.Active;

The club's "today": one small service supplies it, so every query and screen agrees:

public DateOnly Today => DateOnly.FromDateTime(

    TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Chicago"));

Mapping checks after the migration is generated:

Every foreign key into Customers, Members and Admins shows onDelete: ReferentialAction.Restrict. None show Cascade, which is EF's default for required relationships.
Members.MemberId and Officers.OfficerId are not identity columns.
Members has no IsCurrent column and no status column.
MemberExpiration, JoinedDate, DeactivatedDate, GrantedDate and RevokedDate are date, not datetime2.
The filtered unique index on OfficerHistory(MemberId) WHERE RevokedDate IS NULL exists.
Decisions
Question
Decision
Reason
Store the status, or work it out?
Work it out from IsActive + MemberExpiration
A stored status goes stale the day dues run out, and SQL Server can't persist a date-based computed column
Enum or lookup table?
C# enum only
Status isn't stored, so a lookup table would have nothing to point at
Global query filter on Member?
No
Status changes with the date, and most member screens need non-active members
Can members be deleted?
No
Officer history and orders must keep pointing at a real person
Rejoining?
Same row: IsActive = 1, new expiration date, JoinedDate kept
MemberId is the CustomerId, so a second row is impossible
Officer whose dues expire?
Title kept, access paused until renewal
Expiring is often a late payment, not a resignation
Officer who leaves the club?
Title closed in the same transaction
An officer must be a current member
Whose "today"?
The club's local date (America/Chicago)
Dues shouldn't lapse at 7 p.m. the evening before because the server runs on UTC


