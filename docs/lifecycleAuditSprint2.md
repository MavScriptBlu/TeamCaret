# Sprint 1 Review: Product CRUD Design Phase
**Cyber Cougars Club Order & Event System (CCOS), "Caret" Jira Project**
**Presented by:** Blue Johnas (Product Owner)
**Team:** Blue Johnas, Seng Lee, Dylan Morzfeld
**Sprint focus:** Interface design + database design for the Product feature (no code yet, instructor's call)

---

## 1. What We Were Actually Trying to Do

Sprint 1 was design-only, no code. Goal was to walk out with a validated UI design and database design for Product CRUD, solid enough to hand straight to Sprint 2 and just build. We picked Product first because it's the simplest full-CRUD thing in our system and it mirrors the original OES assignment pattern, so it doubles as proof we actually get the architecture before we start stacking more on top of it.

## 2. Wireframe Walkthrough (CAR-2, CAR-3, CAR-4)

**Officer: Product List**
- Filterable/searchable table (All / Active / Inactive)
- Out-of-stock rows get flagged visually so you're not hunting for them
- Inactive rows show greyed out with a "Reactivate" button instead of "Deactivate." There is no hard-delete button anywhere in this UI, full stop

**Officer: Create/Edit Form**
- Required fields marked, validation kicks in on blur/submit
- Member Price field enforces "can't be higher than Price" as a UI rule
- Active toggle is hidden on Create (new products default active), shows up on Edit

**Customer: Product Browse**
- Only active products show up, obviously
- Member pricing only kicks in for customers with a *current* Member record (`IsCurrent = true`). Guests and lapsed members just see regular price
- "Add to Cart" disables at zero stock instead of hiding the product entirely. We want people to see it's out, not think it never existed

## 3. Database Structure: What We Locked In (CAR-5, CAR-6, CAR-7)

- **No schema changes needed.** Product reuses the `Products` table we already built in the ERD/schema, as-is.
- **Soft delete, not hard delete.** "Delete" in the officer UI actually maps to `IsActive = 0`, never an actual row delete. Reasoning's simple: `OrderLines.ProductId` points back at `Products`, so yanking a row with order history attached would break past orders. Not happening.
- **API contract's locked:** public GET endpoints for anyone browsing (customer/guest), Officer-only auth required on POST/PUT/DELETE. Server-side validation matches the UI rules: `Price`/`MemberPrice` ≥ 0, `StockQuantity` ≥ 0.
- **EF Core / migration scope checked** against the existing schema. No surprises waiting for us in Sprint 2.

## 4. Open Questions / Stuff We Still Need to Figure Out

- `MemberPrice ≤ Price` is only enforced in the UI and API right now, not at the database level with an actual CHECK constraint. Need to decide as a team if that's good enough or if we want the DB backing it up too.
- A few file/folder names floating around in the design notes (`backend/CCOS.Api`, `shared/CCOS.Shared`, `docs/CyberCougar OES Schema.md`) don't match what's actually in our project docs. Quick sync needed before anyone starts scaffolding in Sprint 2 so we're not accidentally building two different structures.
- Board didn't keep pace with the actual work this sprint. A few tickets (CAR-3, CAR-6, CAR-7) had the design work done and documented before Jira status caught up. Not a blocker, just calling it out: update the board same day as the work gets done, not after.

## 5. What's Next: Sprint 2

Sprint 2 is where this stops being wireframes and turns into actual code:
- Scaffold the `Product` entity class + EF Core `DbContext` mapping
- Write and run the first migration
- Build the API endpoints per the contract above (GET public, POST/PUT/DELETE Officer-only)
- Build the wireframed pages for real: Officer list/form, Customer browse
- Carry the server-side validation over exactly as we speced it this sprint

---
*Proposal, ERD, schema, and backlog are all in the MSTC project alongside this one.*
