# **Project Proposal: Cyber Cougars Club Order & Event System (CCOS)**

**Course:** Software Architecture \- Team Caret \- Agile Team Semester Project **Based on:** Order Entry System (OES) pattern from Assignment 1.1

## **Overview**

CCOS lets Cyber Cougars members (and, for public events, non-member guests) order club swag/resources and purchase tickets to club events, while officers manage products, venues, events, and membership dues status. It follows the same layered flow from the OES assignment (view → API client → Web API → repository → data), scaled up to a full MVC \+ Entity Framework app with authentication and a real database.

## **Problem / Motivation**

The club currently handles swag orders, event sign-ups, and dues tracking informally across Teams and spreadsheets. There's no single record of who's ordered what, who's attending which event, who's actually current on dues, or who's an officer. CCOS gives members one place to order and RSVP, and gives officers one place to manage inventory, events, and membership status.

## **Scope**

**In scope:**

* Member/guest self-service ordering (swag, resources) and event ticket purchasing  
* Officer management of products, venues, and events (including updating event details)  
* Membership dues tracking \- a customer's current/lapsed status gates member-only events and member pricing  
* Officer/admin access derived from membership \- officers are members first, with an extra layer granting admin rights  
* Order cancellation with inventory/ticket rollback  
* Role-based authentication (Guest → Member → Officer)  
* Transactional order placement with rollback on failure (insufficient stock or sold-out event)

**Out of scope (for this semester):**

* Real payment processing (assume free/dues-covered or track "owed" only)  
* Seat-level assignment within a venue (tickets are general admission)  
* Mobile app (responsive web only)

## **Core Entities**

1. **Customer** anyone who can place an order (name, email); base identity for both members and guests  
2. **Member** a Customer who's a club member, with dues status (`DuesPaidThrough`, `IsCurrent`)  
3. **Officer** a Member who's also a club officer; existence of this record is what grants admin access to the app  
4. **Product** orderable swag/resource item, with stock quantity and category  
5. **Venue** a location where events are held  
6. **Event** a club event (name, date/time, venue, capacity, members-only flag)  
7. **Ticket** one admission purchased for an Event, tied to an Order  
8. **Order** a customer's order header (date, status, total) — can include product line items and/or event tickets in one transaction  
9. **OrderLine** line items bridging Order and Product (quantity, unit price snapshot)

Relationships: Customer 1→0..1 Member (a customer may also be a member) · Member 1→0..1 Officer (a member may also be an officer) · Customer 1→\* Order · Order 1→\* OrderLine · Product 1→\* OrderLine · Order 1→\* Ticket · Event 1→\* Ticket · Venue 1→\* Event

## **Key Transactions**

* **Placing an order:** writes to Order, OrderLine (for products), and/or Ticket (for event admission) in one database transaction. If a product's out of stock or an event's sold out, the whole order rolls back \- nothing partially saves.  
* **Canceling an order:** restocks any Products from its OrderLines and releases any Tickets back to the Event's available capacity, in the same transaction.  
* **Updating event details:** officers can edit an Event's date, venue, capacity, or members-only flag; existing Tickets aren't affected, but new sales respect the updated capacity/flag.  
* **Membership gating:** when a customer tries to buy a members-only event ticket or a member-priced product, the system checks their linked Member record's `IsCurrent` status before allowing the purchase.  
* **Officer promotion:** an officer can grant admin access to another current Member by creating their Officer record; a Customer with no Member record can't be made an Officer directly (has to become a Member first).

## **User Roles & Auth**

Access is a chain, not three unrelated roles:

* **Guest** a Customer with no Member record. Can browse public events and purchase tickets to non-members-only events.  
* **Member** a Customer with a current Member record. Everything a Guest can do, plus order swag/resources, buy members-only event tickets, and get member pricing.  
* **Officer/Admin** a Member who also has an Officer record. Gets admin access: manage Products, Venues, Events, and Member dues status; view/cancel any order.

Implemented with ASP.NET Core Identity, role-based authorization on controllers/pages — the app checks for a matching Officer row (not just a role claim) so admin access always traces back to an actual membership record.

## **Tech Stack**

* ASP.NET Core MVC with Entity Framework Core  
* Razor Pages or Blazor for views (team decision)  
* SQL Server (Azure SQL in production)  
* ASP.NET Core Identity for auth  
* Deployed to Azure App Service

## **Milestones**

1. Project proposal \+ ERD \+ backlog (this deliverable)  
2. Database schema \+ EF models \+ migrations  
3. CRUD for Product/Venue/Event (officer side)  
4. Member dues status management \+ officer promotion (officer side)  
5. Ordering flow: swag/resources with transaction \+ rollback  
6. Event ticket purchase flow, including members-only gating and sold-out handling  
7. Order cancellation flow (restock \+ release tickets)  
8. Auth \+ role restrictions wired in  
9. Azure deployment  
10. Polish, testing, demo prep

## **Team**

* Blue Johnas  
* Seng Lee  
* Dylan Morzfeld

