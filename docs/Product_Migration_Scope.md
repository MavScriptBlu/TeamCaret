## Product Migration Scope

### Initial Product Table

The initial Product migration will include:

- ProductId
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

- PRODUCT must exist before ORDERLINE records can reference it.
- No Category lookup table currently exists; this should be confirmed during review.

### Sample Seed Data

- CyberCougar T-Shirt
- CyberCougar Hoodie
- Inactive/retired product

Seed data will test regular pricing, member pricing, inventory, and inactive products.

### Sprint Review Questions

- Should Category become a lookup table?
- Should MemberPrice be required?
- Should zero-dollar Products be allowed?
- Should StockQuantity allow zero?
- Are decimal(10,2) and current field lengths sufficient?

### Verification

- Confirm Product migration creates successfully.
- Confirm pricing precision.
- Confirm inventory values.
- Confirm seed data inserts successfully.
- Confirm inactive Products can remain referenced by historical orders.