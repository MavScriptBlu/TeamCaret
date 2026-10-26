# CCOS Full Project CRUD Lifecycle & Audit Policies

Reference doc for CAR-31. This covers how every entity in CCOS moves through its life (create, read, update, deactivate) and what gets logged along the way. We're not just writing this for Product, we're writing it once so Membership, Treasury, and Events/Tickets can all build on the same rules instead of every sprint inventing its own version of "how do we handle deleting this."

## 1. Core Rule: We Never Actually Delete Anything

Once a record's been referenced by something else (an order, a ticket, a membership row, a transaction), it does not get hard-deleted. Ever. We set this pattern with Product (`IsActive` flag) and it applies the exact same way everywhere else:

- **Create**: row goes in, active by default.
- **Read**: active stuff shows by default on customer-facing views. Officers/admins can flip a toggle to see inactive too.
- **Update**: anything's editable while active, but updating never rewrites history. An OrderLine's `UnitPrice` snapshot doesn't move just because the Product's live price changed later.
- **Deactivate (soft delete)**: flips `IsActive` to false instead of yanking the row. It's still queryable for history/reporting, just filtered out of the default view.
- **Reactivate**: flip it back. Same row, same history, nothing gets recreated.

Why: every table tied to money or membership (`OrderLines`, `TicketRegistrations`, future `TreasuryTransactions`) has a foreign key pointing back to its parent. Hard-delete the Product, Member, or Event and you snap that link. Now you've got orphaned records or broken history. Soft delete keeps everything intact, permanently. It's the same reason you don't rip out a load-bearing wall without putting a beam in first. You plan for what's leaning on it.

## 2. Entity-by-Entity Notes

| Entity | Soft-delete field | Who can deactivate | Notes |
|---|---|---|---|
| Product | `IsActive` | Officer | Locked in Sprint 1/2. Deactivated products still hang onto their past `OrderLines`. |
| Member | `IsActive` + `MemberExpiration` | Admin (leaving the club); expiring is automatic | Expires on its own when `MemberExpiration` passes and comes back when they renew. Leaving the club is an admin setting `IsActive` to false. Status (Active/Expired/Inactive) is worked out from both, never stored. See Membership Lifecycle & Deactivation Schema. |
| Officer | `Officers` row closed, `OfficerHistory.RevokedDate` set | Advisor (Admin) | Losing officer status shouldn't nuke the Member record underneath it. You're still a member after you step down. |
| Venue | `IsActive` | Officer | Deactivating a venue doesn't touch Events that already happened there; it just can't host new ones. |
| Event | `IsActive` | Officer | Same deal as Product. Old ticket registrations still point at the Event that actually happened. |
| Order | `Status` enum (Pending/Fulfilled/Canceled) | Officer | Orders don't get soft-deleted, they transition status instead. Canceled orders stick around for the money trail. |
| Ticket (`TicketRegistrations`) | `Status` enum (Purchased/Canceled) | Officer, maybe Customer self-cancel (TBD) | Same status-transition move as Order instead of a flag. No Reserved status: checkout is one transaction, so there's no hold step. See Event, Ticket & Venue Relational Schema. |

## 3. Audit Policy

### What actually gets logged
Anything that touches money, membership status, or access level needs a trail. That doesn't mean we're bolting on a separate audit table for everything right out the gate. It means every entity above is built so its own history *is* the audit trail (status changes, soft-delete flags with timestamps) instead of us needing some extra log to piece together what happened after the fact.

Money's the one place that needs more than status flags. Treasury (Sprint 6) gets its own dedicated audit log. Every edit or void needs who changed it, when, and what it looked like before. That's its own ticket (CAR-26) instead of us trying to cram it into the current schema.

### What every audit record needs, minimum
- Who did it: tied to the actual Customer/Member/Officer record, not just a name typed in somewhere
- What changed: before and after, not just "record updated" (that tells us nothing)
- When: full timestamp, not just a date
- Why: required on money voids/edits specifically, optional everywhere else

### Who sees the audit stuff
- Officers see audit history for whatever they manage: Product changes, Order/Ticket status.
- Treasury audit logs (once built) are Officer-visible, but voids/edits on money specifically should get a second officer's eyes on it. Not full approval necessarily, just visibility. It's club money, more than one person should know when it moves.
- Troy (Advisor) gets the same read access as an Officer. That's already how the ERD sets up the access model. Advisor sits next to Officer, not under it.

### Retention
We don't purge audit history as part of normal ops, period. If storage ever actually becomes a problem (unlikely at club scale), that's a manual call an Officer makes, not something that runs on its own.

## 4. Why This Matters Heading Into Sprint 3+

Membership & Officer Roles (Sprint 3) is the first feature after Product that actually needs lifecycle rules past a basic on/off flag, since Officer status hangs off Member status, and Member status is worked out from an admin-set flag plus the dues expiration date. Getting this written down now, before Treasury and Events get built, means those sprints just extend a pattern that already exists instead of everybody reinventing "how do we handle deleting/canceling this" from scratch every time.
