# CAR-90: Sprint 5 Demo Walkthrough

**Issue:** CAR-90 · **Author:** Dylan Morzfeld · **Last updated:** 2026-11-15 · **For:** Sprint 5 review

A click path through everything Sprint 5 built. Sprint 5 goal: polish what we built and get ready to deploy. Run it on a fresh `Dev` in Docker after the last Sprint 5 PRs merge. Rows marked *(if merged)* depend on a PR that was still in review when this was written.

## Before the demo

1. `git checkout Dev`, then `git pull`
2. `docker compose down -v`, then `docker compose up --build -d`. This gives a fresh database with the sample products, venues, events and logins. `SAMPLE_ACCOUNT_PASSWORD` must be set in `.env`.
3. Open http://localhost:8080 once so the first page load isn't slow during the demo.
4. Make the browser window full width (1024px or wider) so the sidebar shows.
5. Optional, for CAR-59: as an officer, create a product named with 60 "a"s and no spaces, so the wrap fix has something to show.

## Sample logins

All use the password in `.env` (`SAMPLE_ACCOUNT_PASSWORD`).

| Login | Header says | Sees |
| --- | --- | --- |
| officer@ccos.test | Hi, Officer | Member prices and every officer page |
| member@ccos.test | Hi, Member | Member prices |
| guest@ccos.test | Hi, Guest | Regular prices only |
| advisor@ccos.test | Hi, Advisor | Officer pages, regular prices (see CAR-85) |

## Demo click path (about 10 minutes)

### Getting around (CAR-64)

| # | Do this | Show this |
| --- | --- | --- |
| 1 | Signed out, open http://localhost:8080/Products | The links sit in a left sidebar on desktop, with no menu button (from Jason's Sprint 3 question). "Merch" is highlighted. |
| 2 | Narrow the window below 1024px, then widen it again | On small screens the menu button and slide-out menu come back, with the same links |

### Merch list and member prices (CAR-75, CAR-78, CAR-59)

| # | Do this | Show this |
| --- | --- | --- |
| 3 | Merch, signed out | Compact header, products visible right away, "4 items", category chips, Sort, 4 cards per row |
| 4 | Click the **Swag** chip, then **Clear filters** | "Showing 3 of 4 items · Clear filters", then everything back |
| 5 | **Log in** as member@ccos.test, Sort: "Price: low to high" | Big maroon member prices with a gold "Member price" tag and the regular price crossed out (CAR-78). Sorted by what a member pays. |
| 6 | Click **Club Hoodie** | Same price look on the product page |

### Events list (CAR-76)

| # | Do this | Show this |
| --- | --- | --- |
| 7 | Sidebar: **Events** (still as the member) | Date block on each card, time, venue, one line of the description, and the member price at the bottom. The Hackathon has a "Members only" tag. |
| 8 | Click **Capture the Flag Night** | The event page shows the same price as its card |
| 9 | **Log out**, open Events again | Regular prices only; the Hackathon says "Log in as a member to see the price" |

### Officer pages (CAR-77, CAR-79, CAR-81, CAR-59)

| # | Do this | Show this |
| --- | --- | --- |
| 10 | **Log in** as officer@ccos.test | The sidebar adds an "Officers" group |
| 11 | Sidebar: **Manage venues** *(if CAR-77 #201 merged)* | A card per venue: address, "Holds …", upcoming events, and an "In the list" or "Removed" tag. Chips with counts. |
| 12 | Look at **Old Gym** | Greyed out, with the "Removed" banner above its details instead of covering them (CAR-81) |
| 13 | On **Cyber Lab**, click **Remove venue** | The dialog warns that 2 upcoming events are booked there and they keep the venue. Click **No** (CAR-79: warn, don't block). |
| 14 | On **Cougar Commons**, click **Remove venue** | Only Capture the Flag Night is listed; the past Fall Kickoff Meetup is left out. Click **No**. |
| 15 | Sidebar: **Product dashboard** (if you made the long-name product) | The long name wraps inside the card (CAR-59) |
| 16 | Sidebar: **Create event** | Hint under Description: "The first line shows on the event card…" (CAR-76) |

### Ready to deploy (CAR-84)

| # | Do this | Show this |
| --- | --- | --- |
| 17 | Open http://localhost:8080/health and http://localhost:8081/health | Both say **Healthy**: the site, API and database are working. `docs/DEPLOYMENT.md` is ready for whichever host Troy picks. |

## Teammates' Sprint 5 work

Ask each owner what they want to show.

| Story | Owner | Status (Nov 15) | What to show |
| --- | --- | --- | --- |
| CAR-80 Edit event overwrite bug | Seng | In review (#200) | Two tabs editing the same event: the second save shows "changed by someone else" with a Reload link |
| CAR-82 CI fix and ZIP validation tests | Blu | Done | Green CI run and the venue validation tests |
| CAR-83 API only accepts bearer tokens | Blu | Done | The decision and saves still working |
| CAR-85 Advisor member prices decision | Blu | To Do | The decision, if made |
| CAR-87 Troy's answers / Sprint 6 focus | Blu | To Do | Hosting/database and payment answers, if in |
| CAR-89 Sprint 5 QA | Seng | To Do | QA results |
| CAR-86 Merge Dev into main | Seng | To Do | After the review |

## Known gaps (don't click on stage)

| Thing | Where | Comes in |
| --- | --- | --- |
| RSVP button | Event page | Sprint 6 (RSVP) |
| Add to cart | Product page | Sprint 6 (cart and checkout) |
| Quick Edit | Officer product dashboard | Not planned; Edit product page does the job |
| News section | Events page | Sample text only; not in the database yet |
| Hosted site | — | After Troy picks the host (CAR-87) |

## Wrap-up

- Sprint 5 goal: polish what we built and get ready to deploy. All three lists from Jason's Sprint 4 notes were revamped, member prices stand out, the venue warning is in, and the app is ready for any host.
- Next: Sprint 6 builds RSVP and the cart/checkout (CAR-88, waiting on Troy's answers).
