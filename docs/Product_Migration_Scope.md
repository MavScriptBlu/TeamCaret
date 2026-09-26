## Product Migration Scope

### Initial Product Table

The initial Product migration will include:

- ProductId
- EventId (nullable FK to Event)
- Category
- Name
- Description
- Price
- MemberPrice
- StockQuantity
- IsActive

### Naming Conventions

- Primary keys use [Entity]Id.
- Foreign keys use [Entity]Id.
- Boolean fields use Is[State].
- Monetary values use decimal(10,2).
- Dates use descriptive PascalCase names.

### Dependencies

- EVENT must exist before creating ticket Products that reference an Event.
- EventId is nullable because normal Products do not require an Event.
- PRODUCT must exist before ORDERLINE records can reference it.
- No Category lookup table currently exists; this should be confirmed during review.

### Sample Seed Data

- CyberCougar T-Shirt
- CyberCougar Hoodie
- General Meeting Ticket
- Inactive/retired product

Seed data will test regular pricing, member pricing, inventory, inactive products, and Event-linked Products.

### Sprint Review Questions

- Should Category become a lookup table?
- Should MemberPrice be required?
- Should zero-dollar Products be allowed?
- Should StockQuantity allow zero?
- Should Event Tickets remain Products with an optional EventId?
- Are decimal(10,2) and current field lengths sufficient?

### Verification

- Confirm Product migration creates successfully.
- Confirm valid and invalid EventId behavior.
- Confirm pricing precision.
- Confirm inventory values.
- Confirm seed data inserts successfully.
- Confirm inactive Products can remain referenced by historical orders.