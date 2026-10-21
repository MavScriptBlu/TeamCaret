# CAR-15: Product QA Checklist

**Issue:** CAR-15 · **Author:** Dylan Morzfeld · **Last updated:** 2026-10-20 · **Run on:** Thu Oct 22 and Fri Oct 23

Manual end-to-end test of product browse, create, edit and delete. Fill in Result (Pass, Fail or Blocked) as you go; every Fail gets its own bug ticket.

| Acceptance criterion | Covered in |
| --- | --- |
| Browse page tested with in-stock, out-of-stock and inactive items | B1–B6 |
| Create form tested for valid input and validation errors with clean defaults | C1–C9 |
| Edit form tested for pre-filled data loading and updates | D1–D7 |
| Delete flow tested for the confirmation dialog | E1–E3 |
| Delete always soft-deletes (IsActive = false) | E4–E5, D6 |
| Validation and empty-state screens verified | C3–C8, F1–F5 |
| Discovered issues logged as separate bug tickets | Logging bugs |

## Before you start

The create, edit and delete tests need CAR-46 (save endpoints) and CAR-38 (delete dialog) merged into Dev; until then, mark those rows Blocked.

1. `git checkout Dev`, then `git pull`
2. `task reset`, which gives an empty database (it asks before deleting)
3. Run the API (`dotnet run` in `backend/CCOS.Api`). It creates the tables and the 5 sample products:
   - 1 Club Hoodie ($35 / $28 member, 42 in stock)
   - 2 Club T-Shirt ($15 / $12, 30)
   - 3 Sticker Pack ($5 / $3, 0 in stock)
   - 4 Study Guide ($8 / $5, 12, category Resources)
   - 5 Old Mug ($10 / $8, 5, deactivated)
4. Run the site (`dotnet run` in `frontend/CCOS.Web`, http://localhost:5275)
5. Open DBeaver on `localhost:5432` for the database checks

IDs 1–5 assume a fresh `task reset`; if a product was added earlier, its ID will be higher.

## Browse and details

| # | Steps | Expected | Result | Bug |
| --- | --- | --- | --- | --- |
| B1 | Open `/Products` | 4 products: Club Hoodie, Club T-Shirt, Sticker Pack, Study Guide. Old Mug is **not** shown. | | |
| B2 | Look at the Sticker Pack card | Greyed out with an "Out of stock" badge; the others say "In stock" | | |
| B3 | Tick "In stock only" | Sticker Pack disappears; the other 3 stay | | |
| B4 | Set Category to Resources | Only Study Guide shows | | |
| B5 | Open `/Products/Details/1` | Club Hoodie with $35.00 and member price $28.00 | | |
| B6 | Open `/Products/Details/5` (Old Mug, deactivated) | 404 Not Found, the product isn't shown | | |

## Create

On `/Officer/Products/Create`. Each validation row should show its error and save nothing.

| # | Steps | Expected | Result | Bug |
| --- | --- | --- | --- | --- |
| C1 | Open the page | All fields empty, no errors showing, no 0s pre-filled | | |
| C2 | Name "Test Cap", Category Swag, Price 20, Member Price 15, Stock 10, Save | Back on the officer dashboard with "Test Cap was added successfully"; Test Cap also shows on `/Products` | | |
| C3 | Leave Name and Category blank, Save | A required error under each; nothing saved | | |
| C4 | Price −1 | "The regular price must be between 0 and 10000.00" | | |
| C5 | Price 10001 | Same price error | | |
| C6 | Price 20, Member Price 25 | "the member price cannot be greater than the regular price" under Member Price | | |
| C7 | Stock −1 | "The stock quantity must be 0 or greater" | | |
| C8 | Paste 600 characters into Description, and 150 into Name | The fields stop at 500 and 100 characters | | |
| C9 | Price 0, Member Price 0, valid otherwise | Saves; `/Products` shows the price as "Free" | | |

## Edit

| # | Steps | Expected | Result | Bug |
| --- | --- | --- | --- | --- |
| D1 | Open `/Officer/Products/Edit/1` | Form filled with Club Hoodie: Swag, $35.00, $28.00, stock 42, Active ticked | | |
| D2 | Change Price to 30, Save | Dashboard shows "Saved"; `/Products` shows Club Hoodie at $30.00 | | |
| D3 | Set Member Price above Price, Save | Member price error; the saved values don't change | | |
| D4 | Open `/Officer/Products/Edit/999` | "Product not found" page with a link back to products | | |
| D5 | Open `/Officer/Products/Edit/5` (Old Mug) | Loads with Active unticked | | |
| D6 | On Test Cap, untick Active, Save | Gone from `/Products`; on the dashboard as deactivated | | |
| D7 | On Old Mug, tick Active, Save | Old Mug now shows on `/Products` (reactivated) | | |

## Delete

On the officer dashboard (`/Officer/Products`). Delete must never remove the row.

| # | Steps | Expected | Result | Bug |
| --- | --- | --- | --- | --- |
| E1 | Click Delete on Club T-Shirt | A confirmation dialog opens; nothing changes yet | | |
| E2 | Click Cancel in the dialog | Dialog closes; T-Shirt still active everywhere | | |
| E3 | Delete again and confirm | T-Shirt gone from `/Products`; on the dashboard as deactivated | | |
| E4 | In DBeaver: `SELECT "ProductId", "Name", "IsActive" FROM "Products" WHERE "Name" = 'Club T-Shirt';` | One row, `IsActive = false` (soft delete, not removed) | | |
| E5 | Order history | No order pages exist yet, so mark Blocked and note it; E4 proves the row stays for past orders | | |

## Empty states, filters and errors

| # | Steps | Expected | Result | Bug |
| --- | --- | --- | --- | --- |
| F1 | `/Officer/Products?show=inactive` | Only deactivated products | | |
| F2 | `/Products` with Category Resources and "In stock only", after setting Study Guide stock to 0 | "No products match these filters." | | |
| F3 | `/Officer/Products?show=inactive` when nothing is deactivated (run after a fresh reset and reactivating Old Mug) | "No products match this filter." | | |
| F4 | Stop the API, reload `/Products` and `/Officer/Products` | "Products couldn't be loaded right now. Please try again later." on both, no crash | | |
| F5 | With the API stopped, Save on the Create form | "The product API could not be reached. Please try again later"; the form keeps what you typed | | |

## Logging bugs

- For each Fail, create a Jira **Bug** titled `[Bug] <page>: <what went wrong>`, link it to CAR-15 with "relates to", and put its key in the Bug column.
- In the bug: the row number (for example C6), steps, expected, what actually happened, and a screenshot.
- Already known, so don't file them: the member price message starts lowercase (C6), and Edit loads the whole product list to find one product.
- When every row is Pass or a noted Blocked, move CAR-15 to Done.
