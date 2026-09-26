# CAR-24: Product Soft-Delete & Lifecycle Data Rules

**Issue:** [CAR-24 / #23](https://github.com/MavScriptBlu/TeamCaret/issues/23) · **Author:** Dylan Morzfeld · **Last updated:** 2026-09-25

**Story:** As a store administrator, I want clear database rules and query filters defined for product deactivation and deletion, so that deactivated items remain preserved for historical orders without cluttering the active storefront.

**In short:** products are never hard-deleted. When an officer (the store administrator in CCOS) deletes a product, it is archived (`IsActive = 0`): it disappears from the storefront but stays linked to every past order. In the CCOS schema, the story's "OrderItem" is the `OrderLines` table.

| Acceptance criterion | Covered in |
| --- | --- |
| Define `IsActive` default values and soft-delete transition logic in EF Core | Section 1 |
| Specify global query filters to exclude inactive products from public browse queries | Section 2 |
| Document cascading rules and FK constraints for historical order line references | Section 3 |
| Notes documented in project architecture space | This doc |

**Stack:** ASP.NET Core Web API with EF Core and SQL Server (Azure SQL in production), per the repo README. This doc defines the rules; it doesn't include implementation code.

| # | Rule | Schema change? |
| --- | --- | --- |
| R1 | `Products.IsActive BIT NOT NULL DEFAULT 1`; new products start active | No, already in schema |
| R2 | "Delete" archives the product (`IsActive = 0`). Officers can't hard-delete products | No |
| R3 | Archived products can be restored (`IsActive = 1`) | No |
| R4 | An EF Core global query filter hides archived products from every product query unless code explicitly opts out | No (EF config) |
| R5 | `FK_OrderLines_Products` stays `ON DELETE NO ACTION` | No, already in schema |
| R6 | `OrderLines` gets `ProductNameSnapshot NVARCHAR(100) NOT NULL`, next to the existing `UnitPrice` snapshot | **Yes** |
| R7 | Order history reads only from `OrderLines`. In EF, `OrderLine` keeps `ProductId` but has no `Product` navigation property | No (EF model) |

## 1. IsActive and soft delete

`IsActive` is required and defaults to 1, so any product inserted without a value (by the app, seed data or a SQL script) starts active. The only way a product leaves the storefront is by being archived.

| From | Officer action | To | Rule |
| --- | --- | --- | --- |
| (new) | Create | Active | Default; can also be saved inactive as a draft |
| Active | Delete (archive) | Inactive | Always allowed, even with stock left or pending orders |
| Inactive | Restore | Active | Always allowed |
| Any | Hard delete | — | Not offered anywhere in the app |

**How it's enforced:** the product repository's delete sets `IsActive = 0` and saves; nothing in the app issues a real `DELETE` on `Products`. As a safety net, a small EF Core save hook (a `SaveChanges` interceptor) turns any accidental delete of a product into an archive.

**Effect on orders:** archiving never touches orders already placed. Pending lines are still fulfilled or canceled normally; only *new* checkouts are blocked.

## 2. Global query filter

Every EF Core query on `Products` automatically adds `WHERE IsActive = 1`, through a global query filter on the Product entity. Code that needs archived products has to opt out explicitly (`IgnoreQueryFilters()`), and only the places marked "Bypassed" below may do that.

| Where products are read | Filter | Why |
| --- | --- | --- |
| Customer browse, category list, product detail | Applied | Customers never see archived items |
| Checkout stock and price check | Applied | Archived products can't be bought; the whole order rolls back with "no longer available" |
| Officer product list, edit, restore | Bypassed | Officers manage archived items |
| Customer order and ticket history, officer all-orders view | Not affected | Reads the saved name and price from `OrderLines` and never touches `Products` |
| Order cancellation (restock) | Bypassed, by product ID | A product archived after the order was placed still gets its stock back |

**Order history can't be broken by the filter.** Every order line already stores what was bought (`ProductNameSnapshot`, `UnitPrice`, `Quantity`), so order history never joins `Products`. To keep it that way, the EF `OrderLine` class has no `Product` navigation property: it keeps `ProductId`, and the foreign key still exists in the database, but there is nothing to follow from an order line to a product. A query that silently drops the order lines of archived products can't be written by mistake.

The only code that reaches a product from an order is cancellation. It looks up each `ProductId` with the filter off to restock it, all in one method.

## 3. Foreign keys and cascade rules

Order history is protected by the database as well as the app: `FK_OrderLines_Products` is `NO ACTION`, so SQL Server refuses to delete any product that appears on an order, even from a script run outside the app.

| Relationship | SQL `ON DELETE` | Effect |
| --- | --- | --- |
| OrderLines → Products | NO ACTION | A product on any order can't be deleted; it can only be archived |
| OrderLines → Orders | CASCADE | Only matters if an order row is deleted. Canceling an order sets `Status = 'Canceled'` and deletes nothing, so order lines stay |

**Snapshots:** each order line stores `UnitPrice` and `ProductNameSnapshot` at checkout. Old orders show what was bought and what it cost, even if the product is later renamed, repriced or archived.

**Implementation note:** EF Core makes required relationships cascade on delete by default. The OrderLine → Product relationship (configured from the Product side, since OrderLine has no navigation) must be set to `Restrict`, and the generated migration should show `onDelete: ReferentialAction.Restrict`. Otherwise deleting a product would wipe its order lines.

Cascade was rejected because it would erase sales history. Set-null was rejected because order lines would lose their product, which cancellation needs to restock.

## Decisions

| Question | Decision | Reason |
| --- | --- | --- |
| Can officers hard-delete products? | No | Archiving covers every case, and order history can never be lost by accident |
| Snapshot the product name on order lines? | Yes, add `ProductNameSnapshot` to `OrderLines` | Matches the existing `UnitPrice` snapshot; renaming a product doesn't rewrite old orders |