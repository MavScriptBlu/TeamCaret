# CAR-42: Link Audit and Demo Script

**Issue:** CAR-42 · **Author:** Dylan Morzfeld · **Last updated:** 2026-10-25 · **For:** Sprint 3 review, Mon Oct 26

A clean click path through everything Sprint 3 built, plus the list of links checked on a fresh `Dev` checkout. Run it in Docker so it matches what the team tested in CAR-15.

## Before the demo

1. `git checkout Dev`, then `git pull`
2. `task reset` (say y), then `task up`. The site is at http://localhost:8080, with the 5 sample products.
3. Open http://localhost:8080 once so the first page load isn't slow during the demo.

## Demo click path (about 5 minutes)

| # | Do this | Show this |
| --- | --- | --- |
| 1 | Open http://localhost:8080 | Home goes straight to Merch (/Products): 4 products, Sticker Pack greyed out as Out of stock, Old Mug hidden |
| 2 | Tick **In stock only**, then set Category to **Resources** | Filters narrow the list; clear them again |
| 3 | Click **Club Hoodie** | Details page with $35.00 and the $28.00 member price |
| 4 | Type `hoodie` in the header search, press Enter | Search results (CAR-36) |
| 5 | Search `fire wood` | "Search for "fire wood" returned no results :(", 2 suggested products, Back to Home |
| 6 | Footer: **Product dashboard** | Officer list of every product, including Old Mug as deactivated |
| 7 | **Create product**: Name "Demo Cap", Category Swag, Price 20, Member Price 25, Save | Member price error shows, nothing saved (CAR-14 validation) |
| 8 | Fix Member Price to 15, Save | Back on the dashboard with "Demo Cap was added successfully"; it also shows on /Products |
| 9 | **Edit product page** on Demo Cap, change Price to 18, Save | "Saved"; the new price shows on /Products |
| 10 | **Delete Product** on Demo Cap, Cancel, then Delete and confirm | Dialog first, then "Product deactivated"; it's gone from /Products but stays on the dashboard (soft delete, CAR-38) |
| 11 | Footer: **Events** | Event pages with sample data (they load from the API in Sprint 4, CAR-50) |

## Link audit (clean `Dev`, Oct 25)

Every link and button below was clicked on a fresh checkout. Result: Pass means no 404 and no error page.

| Page | Links and buttons checked | Result |
| --- | --- | --- |
| Header (every page) | Logo, search box, menu button (opens the drawer) and the 5 drawer links | |
| Footer (every page) | Merch, Events, Product dashboard, Create event, Add venue | |
| `/` | Redirects to /Products | |
| `/Products` | Filters, Apply, each product card | |
| `/Products/Details/{id}` | Back to Products; unknown or inactive id shows 404 | |
| `/Products/Search` | Results, no-results message, Back to Home, suggested cards, empty search | |
| `/Officer/Products` | Create product, Edit product page, Delete Product dialog (Cancel and confirm), the Show filter | |
| `/Officer/Products/Create` and `/Edit/{id}` | Save, validation, Product not found link | |
| `/Events` and `/Events/Details/{id}` | Event cards, Create event, Add venue, Edit, Back | |
| `/Officer/Events/Create`, `/Edit/{id}`, `/Officer/Venues/Create` | Form loads, Save, Back | |
| `/Privacy` | Loads | |

## Placeholders that are fine for this sprint

These do nothing yet on purpose. They're covered by later stories, so they don't need a bug ticket.

| Placeholder | Where | Comes in |
| --- | --- | --- |
| Account (avatar) button | Header | Login, Sprint 4 (CAR-52 stories) |
| Add to cart | Product details | Ordering, Sprint 5 |
| Quick Edit | Officer product dashboard | Not planned yet; Edit product page does the job |
| All event data, RSVP and Cancel event | Events pages | Events API, Sprint 4 (CAR-48, CAR-50) |
| Save on Create event, Edit event and Add venue | Officer event and venue forms | They return to the events page without saving; CAR-51 and Sprint 4 |
| Search covers merch only | Header search ("merch and events") | Events join search after CAR-50 |

Anything else that does nothing, or any 404 or error page, gets its own Jira bug linked to CAR-42.
