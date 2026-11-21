# CAR-100: Sprint 6 Demo Walkthrough

**Issue:** CAR-100 · **Author:** Dylan Morzfeld · **Last updated:** 2026-11-20 · **For:** Sprint 6 review

A click path through everything Sprint 6 built. Sprint 6 goal: members can RSVP to events and order merch and tickets through a cart and checkout (reserve online, pay in person), and officers can see RSVPs and manage orders.

**Draft.** The cart section is final. Sections marked *(when merged)* are outlines from each story's acceptance criteria and get exact steps once that story is merged. Run it on a fresh `Dev` in Docker after the last Sprint 6 PRs merge.

## Who does what

From the Sprint 5 review: one person talks, one person drives. Practice it once before the review.

| Role | Job |
| --- | --- |
| **Presenter** | Talks to the audience, says what each step shows and why it matters, and calls the next step ("Now let's add it to the cart"). Holds this walkthrough. |
| **Steerer** | Drives the mouse and keyboard, follows the presenter's cues, and doesn't narrate. Keeps the sample logins handy. |
| **Story owners** | Step in for their own story if they want to show it themselves (see "Teammates' Sprint 6 work"). |

Tips: the presenter waits for the page to finish loading before talking about it. If something breaks, the steerer says "one sec," and the presenter moves on to the next section.

## Before the demo

1. `git checkout Dev`, then `git pull`
2. `docker compose down -v`, then `docker compose up --build -d`. This gives a fresh database with the sample products, events and logins, and an empty cart and order list. `SAMPLE_ACCOUNT_PASSWORD` must be set in `.env`.
3. Open http://localhost:8080 once so the first page load isn't slow during the demo. If it won't load, use http://127.0.0.1:8080 (another program can grab "localhost:8080" on Windows).
4. Make the browser window full width (1024px or wider) so the sidebar shows.
5. Start signed out with an empty cart.

## Sample logins

All use the password in `.env` (`SAMPLE_ACCOUNT_PASSWORD`).

| Login | Header says | Sees |
| --- | --- | --- |
| officer@ccos.test | Hi, Officer | Member prices and every officer page |
| member@ccos.test | Hi, Member | Member prices, members-only events |
| guest@ccos.test | Hi, Guest | Regular prices only |
| advisor@ccos.test | Hi, Advisor | Officer pages, regular prices |

## Sample data used

| Item | Regular | Member | Notes |
| --- | --- | --- | --- |
| Club Hoodie | $35.00 | $28.00 | 42 in stock |
| Club T-Shirt | $15.00 | $12.00 | 30 in stock |
| Sticker Pack | $5.00 | $3.00 | Out of stock |
| Capture the Flag Night | $10.00 | $5.00 | 50 seats, a week out |
| Intro to Linux Workshop | $5.00 | Free | 30 seats |
| Members-only Hackathon | Free | Free | Members only, 12 of 30 seats taken |

## Demo click path (about 12 minutes)

### Cart (CAR-92)

| # | Do this | Show this |
| --- | --- | --- |
| 1 | Signed out, Merch → **Club Hoodie**, Qty 2, **Add to cart** | Green "Added 2 × Club Hoodie to your cart" with **View cart**. "Cart" in the sidebar now has a gold **2** badge. |
| 2 | Open **Sticker Pack** | "Out of stock," and the button can't be clicked |
| 3 | Events → **Capture the Flag Night**, Qty 1, **Add to cart** | Tickets go in the same cart. The badge says **3**. |
| 4 | Sidebar: **Cart** | Each item with its price, Qty, line total, and the order total in the summary box ($80.00) |
| 5 | Change the hoodie to 1, click **Update** | "Cart updated," new totals |
| 6 | **Log in** as member@ccos.test, open **Cart** | Same items, now at member prices with the gold "Member price" tag ($33.00). The cart stays when you log in. |
| 7 | (Optional) Phone width | Each cart line stacks, and the summary moves under the list |

### Checkout (CAR-93) *(when merged)*

| # | Do this | Show this |
| --- | --- | --- |
| 8 | From the cart, click **Checkout** | Review page: the items and total, and that it's reserve online, pay in person |
| 9 | **Place order** | Confirmation page with the order number. The cart is empty and the badge is gone. |
| 10 | (Optional) Try to order more than is left | The page names the item that's short, and nothing is charged or saved |

### My orders and RSVPs (CAR-95) *(when merged)*

| # | Do this | Show this |
| --- | --- | --- |
| 11 | Open **My orders** | The order from step 9: date, items, total, status **Pending** (pay in person) |

### RSVP (CAR-94) *(when merged)*

| # | Do this | Show this |
| --- | --- | --- |
| 12 | Still as the member, Events → **Intro to Linux Workshop** (free for members), **RSVP** | "You're going," right away |
| 13 | Click **Cancel RSVP** | The seat is given back |
| 14 | **Log out**, open **Members-only Hackathon** | Shows why a non-member can't RSVP |
| 15 | (Optional) A paid event, **RSVP** | Adds a ticket to the cart instead |

### Officer views (CAR-96, CAR-97, Seng) *(when merged)*

| # | Do this | Show this |
| --- | --- | --- |
| 16 | **Log in** as officer@ccos.test → officer orders list | The member's order from step 9. Mark it **paid**, then **picked up**. |
| 17 | Officer "who's coming" view for an event | Everyone with a ticket or RSVP for that event |

### Hosting (CAR-98, CAR-53, Blu) *(if done)*

| # | Do this | Show this |
| --- | --- | --- |
| 18 | Open the Azure site | The same app running online. `/health` says **Healthy**. |

## Teammates' Sprint 6 work

Ask each owner what they want to show.

| Story | Owner | Status (Nov 20) | What to show |
| --- | --- | --- | --- |
| CAR-91 Place an order (API) | Blu | In review (#211) | One order for merch and tickets: server-side prices, all-or-nothing, race test |
| CAR-96 Officer orders list | Seng | In Progress | Steps 16 |
| CAR-97 Who's coming | Seng | To Do | Step 17 |
| CAR-98 Deploy to Azure | Blu | To Do | Step 18 |
| CAR-53 Azure database | Blu | To Do, waiting on access | The shared database |
| CAR-99 Sprint 6 QA | Seng | To Do | QA results |
| CAR-87 Troy's answers | Blu | Done | Azure confirmed, pay in person, storefront focus |

## Known issues (don't click on stage)

| Thing | Where | Why |
| --- | --- | --- |
| "Seats left" counts capacity only | Event page, cart | Until checkout uses orders, tickets already sold don't count against the cart's limit (the order API still checks real seats) |
| Online payment | Checkout | Not in scope: reserve online, pay in person (CAR-87) |
| Cart on another device | Cart | The cart lives in the browser session, so it doesn't follow you to another device, and a site restart empties it |
| News section | Events page | Sample text only; not in the database yet |

Update this list after Sprint 6 QA (CAR-99).

## Wrap-up

- Sprint 6 goal: members can RSVP and order merch and tickets through a cart and checkout, and officers can manage orders and see who's coming.
- Next: final presentation prep, and the hosted site on Azure.
