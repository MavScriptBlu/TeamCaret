# CAR-1: Product CRUD - Design Phase (Sprint 1)

Design-only deliverable for Sprint 1 - interface design + database design for
the **Product** feature. No implementation yet; this doc is meant to be
reviewed in class and used as the spec once coding starts.

Covers the officer-facing user stories from the backlog:

- As an officer, I want to create/edit/delete products, so I can control what's available to order.
- As an officer, I want to set a regular price and a member price per product, so members automatically see their discount.
- As an officer, I want to set/update stock quantity, so availability stays accurate.
- As an officer, I want to deactivate a product without deleting it, so past orders referencing it still display correctly.
- As a customer, I want to browse available products, so I can find what I want to order.
- As a customer, I want to see member vs. regular pricing on products, so I understand what I'll be charged.

## 1. Database Design

Reuses the `Products` table already defined in
`docs/CyberCougar OES Schema.md` and `docs/ERD_ Cyber Cougars Club Order & Event System (CCOS).md`
- no schema changes needed for this feature.

```sql
CREATE TABLE Products (
    ProductId       INT IDENTITY(1,1) PRIMARY KEY,
    Category        NVARCHAR(30)  NOT NULL, -- e.g. 'Swag', 'Resources'
    Name            NVARCHAR(100) NOT NULL,
    Description     NVARCHAR(500) NULL,
    Price           DECIMAL(10,2) NOT NULL CHECK (Price >= 0),
    MemberPrice     DECIMAL(10,2) NOT NULL CHECK (MemberPrice >= 0),
    StockQuantity   INT           NOT NULL CHECK (StockQuantity >= 0),
    IsActive        BIT           NOT NULL DEFAULT 1
);
```

### Field notes for CRUD

| Field           | Required on create | Editable | Notes                                                                 |
|-----------------|---------------------|----------|------------------------------------------------------------------------|
| ProductId       | generated           | no       | identity PK                                                            |
| Category        | yes                 | yes      | free text for Sprint 1; could become a lookup table later              |
| Name            | yes                 | yes      | max 100 chars                                                          |
| Description     | no                  | yes      | max 500 chars                                                          |
| Price           | yes                 | yes      | must be >= 0                                                            |
| MemberPrice     | yes                 | yes      | must be >= 0; convention is MemberPrice <= Price, enforced in UI/API   |
| StockQuantity   | yes                 | yes      | must be >= 0; "Delete" from the officer's perspective sets this via IsActive, not a row delete |
| IsActive        | defaults to true    | yes (toggle) | "soft delete" flag - inactive products stay in the DB so historical `OrderLines` still resolve |

**Delete = deactivate, not a row delete.** Because `OrderLines.ProductId`
references `Products`, removing a row that has existing orders would break
order history. The officer "Delete" action in the UI maps to
`IsActive = 0`, and the product list/browse queries filter on `IsActive` as
appropriate (see below).

## 2. API Design (for `backend/CCOS.Api`, implemented in a later sprint)

DTOs live in `shared/CCOS.Shared` per the project structure; entities stay in
`backend/CCOS.Data`.

```csharp
// shared/CCOS.Shared
public record ProductDto(
    int ProductId,
    string Category,
    string Name,
    string? Description,
    decimal Price,
    decimal MemberPrice,
    int StockQuantity,
    bool IsActive);

public record CreateProductRequest(
    string Category,
    string Name,
    string? Description,
    decimal Price,
    decimal MemberPrice,
    int StockQuantity);

public record UpdateProductRequest(
    string Category,
    string Name,
    string? Description,
    decimal Price,
    decimal MemberPrice,
    int StockQuantity,
    bool IsActive);
```

| Endpoint                     | Method | Auth            | Description                                      |
|------------------------------|--------|------------------|---------------------------------------------------|
| `/api/products`              | GET    | any (public)     | List active products (customer/guest browsing)     |
| `/api/products?includeInactive=true` | GET | Officer     | List all products, including deactivated ones      |
| `/api/products/{id}`         | GET    | any (public)     | Get one product                                    |
| `/api/products`              | POST   | Officer          | Create a product                                   |
| `/api/products/{id}`         | PUT    | Officer          | Update a product (including `IsActive`)            |
| `/api/products/{id}`         | DELETE | Officer          | Deactivate a product (`IsActive = 0`); not a hard delete |

### Validation rules (client- and server-side, per backlog)

- `Name` and `Category` required, non-empty.
- `Price` and `MemberPrice` >= 0.
- `MemberPrice` should not exceed `Price` (warning in UI, enforced as a rule
  on save).
- `StockQuantity` >= 0.
- Customer-facing GET endpoints only ever return products where
  `IsActive = 1`; officer screens can show both.

## 3. Interface Design

### 3.1 Officer: Product List (`/officer/products`)

```
+--------------------------------------------------------------------+
| Products                                          [+ New Product]  |
+--------------------------------------------------------------------+
| Filter: [All ▾] (All / Active / Inactive)   Search: [___________]  |
+--------------------------------------------------------------------+
| Name          | Category | Price  | Member $ | Stock | Active | .. |
|---------------|----------|--------|-----------|-------|--------|----|
| Club Hoodie   | Swag     | $35.00 | $28.00    |  42   |  Yes   | [Edit] [Deactivate] |
| Sticker Pack  | Swag     | $5.00  | $3.00     |   0   |  Yes   | [Edit] [Deactivate] |
| Old Mug       | Swag     | $10.00 | $8.00     |   5   |  No    | [Edit] [Reactivate] |
+--------------------------------------------------------------------+
```

- Rows with `StockQuantity = 0` are visually flagged ("Out of stock").
- Inactive rows are shown greyed out with a "Reactivate" action instead of
  "Deactivate".
- "Deactivate"/"Reactivate" call `PUT /api/products/{id}` toggling
  `IsActive`; there is no hard-delete button in the UI, matching the
  soft-delete design above.

### 3.2 Officer: Create / Edit Product Form

```
+--------------------------------------------------------------------+
| New Product                                                        |
+--------------------------------------------------------------------+
| Name *          [_______________________________]                 |
| Category *      [_______________________________]                 |
| Description     [_______________________________]                 |
|                 [_______________________________]                 |
| Price *         [ $______ ]                                        |
| Member Price *  [ $______ ]   (must be <= Price)                   |
| Stock Quantity *[ ______ ]                                          |
| Active          [x] Active                                          |
|                                                                      |
|                                   [Cancel]   [Save Product]         |
+--------------------------------------------------------------------+
```

- `*` marked fields required; inline validation messages appear under each
  field on blur/submit, mirroring the server-side rules above.
- The "Active" checkbox is hidden on the **Create** form (defaults to
  active) and shown on **Edit** so officers can deactivate from the same
  form as the list-page action.

### 3.3 Customer: Product Browse (`/products`)

```
+--------------------------------------------------------------------+
| Products                                                            |
+--------------------------------------------------------------------+
| [Category: All ▾]                                                   |
+--------------------------------------------------------------------+
|  Club Hoodie                        |  Sticker Pack                 |
|  Swag                                |  Swag                        |
|  $35.00  (Member price: $28.00)      |  $5.00  (Member price: $3.00)|
|  In stock                            |  Out of stock                |
|  [Add to Cart]                       |  [Add to Cart] (disabled)     |
+--------------------------------------------------------------------+
```

- Only `IsActive = 1` products are listed.
- Member pricing is shown only when the logged-in customer has a current
  `Member` record (`IsCurrent = true`); guests and lapsed members only see
  `Price`.
- "Add to Cart" is disabled when `StockQuantity = 0`, showing "Out of
  stock" instead.

## 4. Out of Scope for This Sprint

- No actual controllers/pages/migrations are implemented yet - see
  `backend/README.md` and `frontend/README.md` for where this design lands
  once coding starts.
- Category is plain text for now; promoting it to a lookup table is a
  possible future refinement, not part of this design.
