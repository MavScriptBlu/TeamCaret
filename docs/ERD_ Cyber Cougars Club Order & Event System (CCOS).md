# **ERD: Cyber Cougars Club Order & Event System (CCOS)**

mermaid  
erDiagram  
    CUSTOMER ||--o| MEMBER : "may be"  
    MEMBER ||--o| OFFICER : "may be"  
    CUSTOMER ||--o{ ORDER : places  
    ORDER ||--o{ ORDERLINE : contains  
    PRODUCT ||--o{ ORDERLINE : "ordered as"  
    ORDER ||--o{ TICKET : includes  
    EVENT ||--o{ TICKET : "admits via"  
    VENUE ||--o{ EVENT : hosts

Table CUSTOMER {
    CustomerId int [pk, increment]
    FirstName varchar
    LastName varchar
    Email varchar
}

Table MEMBER {
    MemberId int [pk, ref: - CUSTOMER.CustomerId]
    DuesPaidThrough date
    IsCurrent boolean
}

Table OFFICER {
    OfficerId int [pk, ref: - MEMBER.MemberId]
    Title varchar
    GrantedDate date
}

Table PRODUCT {
    ProductId int [pk, increment]
    Category varchar // Swag or Resources
    Name varchar
    Description varchar
    Price decimal
    MemberPrice decimal
    StockQuantity int
    IsActive boolean
}

Table VENUE {
    VenueId int [pk, increment]
    Name varchar
    Address varchar
    Capacity int
}

Table EVENT {
    EventId int [pk, increment]
    VenueId int [ref: > VENUE.VenueId]
    Name varchar
    Description varchar
    EventDateTime datetime
    TicketCapacity int
    MembersOnly boolean
    Price decimal
    MemberPrice decimal
}

Table ORDER {
    OrderId int [pk, increment]
    CustomerId int [ref: > CUSTOMER.CustomerId]
    OrderDate datetime
    Status varchar // Pending, Fulfilled, Canceled
    TotalAmount decimal
}

Table ORDERLINE {
    OrderLineId int [pk, increment]
    OrderId int [ref: > ORDER.OrderId]
    ProductId int [ref: > PRODUCT.ProductId]
    Quantity int
    UnitPrice decimal
}

Table TICKET {
    TicketId int [pk, increment]
    OrderId int [ref: > ORDER.OrderId]
    EventId int [ref: > EVENT.EventId]
    PriceCharged decimal
    Status varchar // Reserved, Purchased, Canceled
}


## **Access Chain: Customer → Member → Officer**

This is the key relationship for authentication/authorization: it's a chain, not three separate roles bolted onto one table.

* **Customer** — anyone with an account (guest or member). Can place orders, buy non-members-only tickets.  
* **Member** — a Customer who's also in the Members table (current dues). Unlocks member pricing and members-only events.  
* **Officer** — a Member who's also in the Officers table. Every Officer is necessarily a Member (you can't skip the chain), and being in this table is what grants admin access to the app — product/venue/event management, viewing all orders, managing member dues status.

Checking access at login is just: does this CustomerId have a matching row in Officers? → admin. Only in Members? → member pricing/events. Neither? → guest.

## **Relationship Notes**

* **Customer → Member**: optional one-to-one. Every Member is a Customer, but not every Customer is a Member.  
* **Member → Officer**: optional one-to-one. Every Officer is a Member, but not every Member is an Officer.  
* **Customer → Order**: one-to-many. Either a Member or a Guest customer can place orders.  
* **Order → OrderLine → Product**: same bridge pattern as the original OES example — resolves the many-to-many between Order and Product.  
* **Order → Ticket**: one-to-many. A single order/checkout can include multiple event tickets alongside (or instead of) product line items.  
* **Event → Ticket**: one-to-many. Each ticket admits to exactly one event.  
* **Venue → Event**: one-to-many. A venue can host many events over the semester.

## **Field Notes**

* `IsCurrent` on Member is either a computed property (`DuesPaidThrough >= today`) or a maintained flag officers can override — team's call, computed is less error-prone.  
* `MemberPrice` fields on Product and Event let the ordering flow apply member pricing automatically when `Customer.Member.IsCurrent` is true; if there's no linked Member, only the regular `Price` applies.  
* `MembersOnly` on Event blocks ticket purchase entirely for customers without a current Member record — check this before the price check.  
* `PriceCharged` on Ticket snapshots what was actually paid (member or regular price) at purchase time, same reasoning as `UnitPrice` on OrderLine — don't rely on live Event pricing for historical tickets.  
* Canceling an Order should restock any related Product quantities (via its OrderLines) and set related Tickets to `Canceled`, freeing that capacity back on the Event — do both in the same transaction as the cancellation.  
* Only current Members should be promotable to Officer in the UI — worth a validation rule, not just a DB constraint.

