#!/usr/bin/env bash
# Creates the CCOS user stories as GitHub issues.
#
# WHAT IT DOES: makes labels, then makes one issue per user story.
# WHAT IT NEVER DOES: edit, close, or delete anything. It only adds.
# SAFE TO RE-RUN: any story whose 'US-XX:' title already exists is skipped.
#
# USAGE (in Git Bash, from anywhere):
#   ./create_issues.sh        -> PREVIEW ONLY, creates nothing
#   ./create_issues.sh go     -> actually creates the issues

REPO="MavScriptBlu/TeamCaret"
MODE="${1:-preview}"

if [ "$MODE" = "go" ]; then
  command -v gh >/dev/null 2>&1 || { echo "GitHub CLI (gh) not found. Install it from https://cli.github.com then run: gh auth login"; exit 1; }
  gh auth status >/dev/null 2>&1 || { echo "Not logged in. Run: gh auth login"; exit 1; }
  EXISTING="$(gh issue list --repo "$REPO" --state all --limit 1000 --json title -q ".[].title")"
else
  echo "PREVIEW MODE - nothing will be created. Run with: ./create_issues.sh go"
  echo
  EXISTING=""
fi

CREATED=0; SKIPPED=0

# ---------- labels ----------
make_label() {
  [ "$MODE" = "go" ] || { echo "[label] $1"; return; }
  gh label create "$1" --repo "$REPO" --color "$2" --description "$3" --force >/dev/null
}
make_label "user-story" "7A1F2B" "A user story"
make_label "epic: Products" "1D76DB" "Sprint 2 (Jira CAR-10)"
make_label "epic: Accounts & Membership" "0E8A16" "Sprint 3 (Jira CAR-18)"
make_label "epic: Officers & Roles" "5319E7" "Sprint 3 (Jira CAR-18)"
make_label "epic: Orders & Checkout" "FBCA04" "Sprint 4 (Jira CAR-19)"
make_label "epic: Events, Venues & Tickets" "D93F0B" "Sprint 5 (Jira CAR-20)"
make_label "epic: Club Treasury" "0052CC" "Sprint 6 (Jira CAR-21)"
make_label "epic: Hardening, Polish & Launch" "B60205" "Sprint 7 (Jira CAR-22)"
make_label "question" "D876E3" "Needs a team decision"

# ---------- issue maker ----------
make_issue() {
  local id="$1" title="$2" label="$3" body="$4"
  if echo "$EXISTING" | grep -q "^${id}:"; then
    echo "skip  $id (already exists)"; SKIPPED=$((SKIPPED+1)); return
  fi
  if [ "$MODE" = "go" ]; then
    gh issue create --repo "$REPO" --title "${id}: ${title}" --body "$body" --label "user-story" --label "$label" >/dev/null && echo "made  $id" && CREATED=$((CREATED+1))
  else
    echo "would make  $id: $title   [$label]"; CREATED=$((CREATED+1))
  fi
}

make_issue 'US-01' 'Browse products' 'epic: Products' 'As a customer, I want to browse the club'\''s merchandise so I can see what'\''s for sale.

### Acceptance criteria
- [ ] List shows name, category, price, member price, and stock status
- [ ] Inactive products don'\''t show to customers
- [ ] Works on phone and desktop

---
Epic: Products (Sprint 2, Jira CAR-10)'
make_issue 'US-02' 'See member savings' 'epic: Products' 'As a member, I want to see the member price next to the regular price so I know what I'\''m saving.

### Acceptance criteria
- [ ] Both prices visible on every product
- [ ] Member price is highlighted when I'\''m a current member

---
Epic: Products (Sprint 2, Jira CAR-10)'
make_issue 'US-03' 'Know if something is out of stock' 'epic: Products' 'As a customer, I want out-of-stock items clearly marked so I don'\''t waste time trying to buy them.

### Acceptance criteria
- [ ] "Out of stock" badge shown when quantity is 0
- [ ] Out-of-stock items can'\''t be added to an order

---
Epic: Products (Sprint 2, Jira CAR-10)'
make_issue 'US-04' 'Search and filter products' 'epic: Products' 'As a customer, I want to filter by category and search by name so I can find things fast.

### Acceptance criteria
- [ ] Category filter works
- [ ] Name search works
- [ ] Empty-state message shows when nothing matches

---
Epic: Products (Sprint 2, Jira CAR-10)'
make_issue 'US-05' 'Add a product' 'epic: Products' 'As an officer, I want to add a new product so the catalog stays current.

### Acceptance criteria
- [ ] Form captures name, description, category, price, member price, stock, active flag
- [ ] Can'\''t save negative prices or blank names
- [ ] New product shows up in the browse list right away

---
Epic: Products (Sprint 2, Jira CAR-10)'
make_issue 'US-06' 'Edit a product' 'epic: Products' 'As an officer, I want to change a product'\''s details so pricing and stock stay accurate.

### Acceptance criteria
- [ ] Form pre-fills with current values
- [ ] Changes show up right away for customers
- [ ] Old orders keep the price they were bought at

---
Epic: Products (Sprint 2, Jira CAR-10)'
make_issue 'US-07' 'Remove a product' 'epic: Products' 'As an officer, I want to remove a product from sale without losing order history.

### Acceptance criteria
- [ ] Confirmation step before removing (no one-click accidents)
- [ ] Product is deactivated, not hard-deleted, if it'\''s on past orders
- [ ] Deactivated products can be turned back on

---
Epic: Products (Sprint 2, Jira CAR-10)'
make_issue 'US-08' 'Adjust stock' 'epic: Products' 'As an officer, I want to update stock counts quickly so inventory matches what'\''s on the shelf.

### Acceptance criteria
- [ ] Can set a new quantity without editing the whole product
- [ ] Can'\''t go below zero

---
Epic: Products (Sprint 2, Jira CAR-10)'
make_issue 'US-09' 'Friendly errors on product forms' 'epic: Products' 'As an officer, I want clear error messages when I mess up a form so I know what to fix.

### Acceptance criteria
- [ ] Errors show next to the field that'\''s wrong
- [ ] Form keeps what I already typed

---
Epic: Products (Sprint 2, Jira CAR-10)'
make_issue 'US-10' 'Log in with my school Google account' 'epic: Accounts & Membership' 'As a customer, I want to log in with my Google account so I don'\''t need another password.

### Acceptance criteria
- [ ] Google sign-in works
- [ ] First login creates my customer record automatically
- [ ] Returning logins find my existing record, no duplicates

---
Epic: Accounts & Membership (Sprint 3, Jira CAR-18)'
make_issue 'US-11' 'Stay protected' 'epic: Accounts & Membership' 'As a club stakeholder, I want the site to be password/account protected and self-contained so club data isn'\''t open to the public or tied to the school'\''s Active Directory.

### Acceptance criteria
- [ ] Nothing officer/advisor-only is reachable when logged out
- [ ] Login doesn'\''t depend on Mid-State AD

---
Epic: Accounts & Membership (Sprint 3, Jira CAR-18)'
make_issue 'US-12' 'Log out' 'epic: Accounts & Membership' 'As any logged-in user, I want to log out so nobody else can use my session on a shared computer.

### Acceptance criteria
- [ ] Logout ends the session
- [ ] Protected pages redirect to login afterward

---
Epic: Accounts & Membership (Sprint 3, Jira CAR-18)'
make_issue 'US-13' 'Join the club online' 'epic: Accounts & Membership' 'As a student, I want to fill out the member sign-up form on the club page so I can join without printing anything.

### Acceptance criteria
- [ ] Form has the same fields and agreements as the paper version
- [ ] Every agreement box must be ticked to submit
- [ ] Submission reaches Troy (not a dead end)

---
Epic: Accounts & Membership (Sprint 3, Jira CAR-18)'
make_issue 'US-14' 'Get added as a member' 'epic: Accounts & Membership' 'As the advisor, I want to add a person as a member by their school email so their membership is on record once they'\''ve signed up and paid.

### Acceptance criteria
- [ ] Enter name, school email, and dues-paid-through date
- [ ] If they already have an account, membership attaches to it (no duplicate)
- [ ] Member pricing and members-only access start immediately

---
Epic: Accounts & Membership (Sprint 3, Jira CAR-18)'
make_issue 'US-15' 'Renew dues' 'epic: Accounts & Membership' 'As the advisor, I want to update a member'\''s dues date when they pay again so their status stays current.

### Acceptance criteria
- [ ] Changing the date updates member/current status right away
- [ ] Renewal doesn'\''t create a duplicate member

---
Epic: Accounts & Membership (Sprint 3, Jira CAR-18)'
make_issue 'US-16' 'See my own membership status' 'epic: Accounts & Membership' 'As a member, I want to see whether my dues are current and when they run out so I know when to renew.

### Acceptance criteria
- [ ] Shows current/expired and the expiration date

---
Epic: Accounts & Membership (Sprint 3, Jira CAR-18)'
make_issue 'US-17' 'Remove or expire a member' 'epic: Accounts & Membership' 'As the advisor, I want to remove a member (or let them lapse) so the roster stays honest.

### Acceptance criteria
- [ ] Confirmation before removal
- [ ] Removed members lose member pricing and members-only access
- [ ] Their past orders and tickets are kept

---
Epic: Accounts & Membership (Sprint 3, Jira CAR-18)'
make_issue 'US-18' 'Manage my contact info' 'epic: Accounts & Membership' 'As a customer, I want to keep more than one email and phone number on file so the club can reach me the way I prefer.

### Acceptance criteria
- [ ] Can add/remove emails and phones
- [ ] One of each is marked primary
- [ ] Login still works with my primary email

---
Epic: Accounts & Membership (Sprint 3, Jira CAR-18)'
make_issue 'US-19' 'View the member roster' 'epic: Accounts & Membership' 'As an officer, I want to see all members with their dues status so I know who'\''s in good standing.

### Acceptance criteria
- [ ] Shows name, email, dues-through date, current/expired
- [ ] Can sort or filter by current vs. expired

---
Epic: Accounts & Membership (Sprint 3, Jira CAR-18)'
make_issue 'US-20' 'Search the roster' 'epic: Accounts & Membership' 'As an officer or advisor, I want to search people by name or email so I can find someone fast.

### Acceptance criteria
- [ ] Search matches any of a person'\''s emails

---
Epic: Accounts & Membership (Sprint 3, Jira CAR-18)'
make_issue 'US-21' 'Make someone an officer' 'epic: Officers & Roles' 'As the advisor, I want to give a member an officer title so they get officer access.

### Acceptance criteria
- [ ] Pick a member and a title (President, VP, Treasurer, Secretary)
- [ ] Only members can become officers
- [ ] Only the advisor can do this

---
Epic: Officers & Roles (Sprint 3, Jira CAR-18)'
make_issue 'US-22' 'Hand off a title' 'epic: Officers & Roles' 'As the advisor, I want to move a title to someone else mid-year so the club keeps running when someone steps down.

### Acceptance criteria
- [ ] Changing a title is a simple edit, no rebuild
- [ ] The change is logged

---
Epic: Officers & Roles (Sprint 3, Jira CAR-18)'
make_issue 'US-23' 'Take officer access away' 'epic: Officers & Roles' 'As the advisor, I want to revoke someone'\''s officer status so they no longer have officer access.

### Acceptance criteria
- [ ] Confirmation before revoking
- [ ] Access stops right away
- [ ] The person stays a member

---
Epic: Officers & Roles (Sprint 3, Jira CAR-18)'
make_issue 'US-24' 'See who held each title over time' 'epic: Officers & Roles' 'As the advisor, I want a history of who held which title and when so we can answer "who was Treasurer last spring?"

### Acceptance criteria
- [ ] Every grant, change, and revoke is recorded with dates and who did it
- [ ] History stays even after someone is revoked

---
Epic: Officers & Roles (Sprint 3, Jira CAR-18)'
make_issue 'US-25' 'All officers get the same access' 'epic: Officers & Roles' 'As an officer, I want the same access as every other officer so I can cover for someone who'\''s out.

### Acceptance criteria
- [ ] Any officer can manage products, events, orders, and treasury
- [ ] Access doesn'\''t depend on which title I hold
- [ ] Only the advisor can grant/revoke roles

---
Epic: Officers & Roles (Sprint 3, Jira CAR-18)'
make_issue 'US-26' 'See the current officers' 'epic: Officers & Roles' 'As a member, I want to see who the current officers are so I know who to talk to.

### Acceptance criteria
- [ ] Shows name and title only (no private contact info)

---
Epic: Officers & Roles (Sprint 3, Jira CAR-18)'
make_issue 'US-27' 'Officers can'\''t change roles' 'epic: Officers & Roles' 'As the advisor, I want to be sure officers can'\''t grant or remove roles so control of access stays with me.

### Acceptance criteria
- [ ] Role screens reject officers, not just hide the buttons

---
Epic: Officers & Roles (Sprint 3, Jira CAR-18)'
make_issue 'US-28' 'Advisor account is separate' 'epic: Officers & Roles' 'As the advisor, I want my own account that isn'\''t part of the member/officer chain so I'\''m not treated like a student member.

### Acceptance criteria
- [ ] Advisor has full access without needing dues or an officer title

---
Epic: Officers & Roles (Sprint 3, Jira CAR-18)'
make_issue 'US-29' 'Start an order' 'epic: Orders & Checkout' 'As a customer, I want to add products to an order with quantities so I can buy several things at once.

### Acceptance criteria
- [ ] Can add, change quantity, and remove items
- [ ] Running total updates as I go

---
Epic: Orders & Checkout (Sprint 4, Jira CAR-19)'
make_issue 'US-30' 'Place an order' 'epic: Orders & Checkout' 'As a customer, I want to submit my order so the club knows what I want.

### Acceptance criteria
- [ ] Must be logged in
- [ ] Order is saved with status Pending
- [ ] I see a confirmation

---
Epic: Orders & Checkout (Sprint 4, Jira CAR-19)'
make_issue 'US-31' 'Get the right price automatically' 'epic: Orders & Checkout' 'As a current member, I want member pricing applied without doing anything so I always get my discount.

### Acceptance criteria
- [ ] Current members pay member price
- [ ] Non-members and lapsed members pay regular price
- [ ] The price is locked in when I order

---
Epic: Orders & Checkout (Sprint 4, Jira CAR-19)'
make_issue 'US-32' 'Can'\''t order more than what'\''s in stock' 'epic: Orders & Checkout' 'As a customer, I want to be told if there isn'\''t enough stock so I don'\''t order something that can'\''t be filled.

### Acceptance criteria
- [ ] Quantity is capped at available stock
- [ ] Stock goes down when the order is placed

---
Epic: Orders & Checkout (Sprint 4, Jira CAR-19)'
make_issue 'US-33' 'See my past orders' 'epic: Orders & Checkout' 'As a customer, I want to see my order history so I can check what I bought and its status.

### Acceptance criteria
- [ ] Shows date, items, quantities, total, status
- [ ] I only see my own orders

---
Epic: Orders & Checkout (Sprint 4, Jira CAR-19)'
make_issue 'US-34' 'See all orders' 'epic: Orders & Checkout' 'As an officer, I want to see every order so I know what needs to be handed out.

### Acceptance criteria
- [ ] List with customer, items, total, status
- [ ] Filter by status

---
Epic: Orders & Checkout (Sprint 4, Jira CAR-19)'
make_issue 'US-35' 'Mark an order fulfilled' 'epic: Orders & Checkout' 'As an officer, I want to mark an order as fulfilled so everyone knows it'\''s been handed over.

### Acceptance criteria
- [ ] Status changes to Fulfilled
- [ ] Customer sees the new status

---
Epic: Orders & Checkout (Sprint 4, Jira CAR-19)'
make_issue 'US-36' 'Cancel an order' 'epic: Orders & Checkout' 'As a customer or officer, I want to cancel an order so mistakes don'\''t stick around.

### Acceptance criteria
- [ ] Customer can cancel their own Pending orders
- [ ] Officer can cancel any Pending order
- [ ] Cancelling puts the stock back

---
Epic: Orders & Checkout (Sprint 4, Jira CAR-19)'
make_issue 'US-37' 'Browse upcoming events' 'epic: Events, Venues & Tickets' 'As a customer, I want to see upcoming events with date, place, and price so I can pick what to go to.

### Acceptance criteria
- [ ] Shows name, date/time, venue, price, member price
- [ ] Members-only events are labeled
- [ ] Past events are hidden from the main list

---
Epic: Events, Venues & Tickets (Sprint 5, Jira CAR-20)'
make_issue 'US-38' 'See event details' 'epic: Events, Venues & Tickets' 'As a customer, I want an event'\''s full details, including the venue and address, so I know where to show up.

### Acceptance criteria
- [ ] Description, venue name, address, and spots left

---
Epic: Events, Venues & Tickets (Sprint 5, Jira CAR-20)'
make_issue 'US-39' 'Get a ticket' 'epic: Events, Venues & Tickets' 'As a customer, I want to get a ticket to an event so I have a spot.

### Acceptance criteria
- [ ] Must be logged in
- [ ] Ticket is saved on an order
- [ ] I see a confirmation

---
Epic: Events, Venues & Tickets (Sprint 5, Jira CAR-20)'
make_issue 'US-40' 'Members-only events stay members-only' 'epic: Events, Venues & Tickets' 'As an officer, I want members-only events to reject non-members so those events stay exclusive.

### Acceptance criteria
- [ ] Non-members and lapsed members can'\''t get a ticket
- [ ] They see a clear reason why

---
Epic: Events, Venues & Tickets (Sprint 5, Jira CAR-20)'
make_issue 'US-41' 'Member ticket pricing' 'epic: Events, Venues & Tickets' 'As a current member, I want member pricing on tickets automatically so I don'\''t have to ask.

### Acceptance criteria
- [ ] Price is locked in when the ticket is bought

---
Epic: Events, Venues & Tickets (Sprint 5, Jira CAR-20)'
make_issue 'US-42' 'Events can sell out' 'epic: Events, Venues & Tickets' 'As an officer, I want ticket sales to stop at capacity so we never oversell a room.

### Acceptance criteria
- [ ] Purchase is blocked when the event is full
- [ ] Cancelled tickets free the spot back up

---
Epic: Events, Venues & Tickets (Sprint 5, Jira CAR-20)'
make_issue 'US-43' 'See my tickets' 'epic: Events, Venues & Tickets' 'As a customer, I want to see my tickets so I know what I'\''ve got coming up.

### Acceptance criteria
- [ ] Shows event, date, price paid, status

---
Epic: Events, Venues & Tickets (Sprint 5, Jira CAR-20)'
make_issue 'US-44' 'Cancel a ticket' 'epic: Events, Venues & Tickets' 'As a customer, I want to cancel my ticket so my spot goes to someone else.

### Acceptance criteria
- [ ] Ticket status becomes Canceled
- [ ] Spot is freed

---
Epic: Events, Venues & Tickets (Sprint 5, Jira CAR-20)'
make_issue 'US-45' 'Create and edit events' 'epic: Events, Venues & Tickets' 'As an officer, I want to add and change events so the calendar stays accurate.

### Acceptance criteria
- [ ] Form for name, description, date/time, venue, capacity, prices, members-only flag
- [ ] Can'\''t set capacity above the venue'\''s capacity
- [ ] Changes show up right away

---
Epic: Events, Venues & Tickets (Sprint 5, Jira CAR-20)'
make_issue 'US-46' 'Cancel or remove an event' 'epic: Events, Venues & Tickets' 'As an officer, I want to cancel an event without erasing who bought tickets.

### Acceptance criteria
- [ ] Confirmation first
- [ ] Existing ticket holders are still on record

---
Epic: Events, Venues & Tickets (Sprint 5, Jira CAR-20)'
make_issue 'US-47' 'Manage venues' 'epic: Events, Venues & Tickets' 'As an officer, I want to add, edit, and remove venues so events can be tied to real places.

### Acceptance criteria
- [ ] Name, address, capacity
- [ ] A venue with events can'\''t be deleted outright

---
Epic: Events, Venues & Tickets (Sprint 5, Jira CAR-20)'
make_issue 'US-48' 'See how full an event is' 'epic: Events, Venues & Tickets' 'As an officer, I want to see tickets sold vs. capacity so I can plan.

### Acceptance criteria
- [ ] Sold, remaining, and cancelled counts per event

---
Epic: Events, Venues & Tickets (Sprint 5, Jira CAR-20)'
make_issue 'US-49' 'Set the starting balance' 'epic: Club Treasury' 'As an officer, I want to enter the club'\''s balance as of the day we start tracking so the running total is right.

### Acceptance criteria
- [ ] One starting balance and "as of" date
- [ ] Changing it updates the running total

---
Epic: Club Treasury (Sprint 6, Jira CAR-21)'
make_issue 'US-50' 'Log an expense' 'epic: Club Treasury' 'As an officer, I want to log what the club spent (type, date, amount, description) so there'\''s a record.

### Acceptance criteria
- [ ] Type is picked from a list
- [ ] Amount can'\''t be negative
- [ ] Shows on the dashboard right away

---
Epic: Club Treasury (Sprint 6, Jira CAR-21)'
make_issue 'US-51' 'Log a deposit' 'epic: Club Treasury' 'As an officer, I want to log money coming in (dues, ticket cash, fundraisers) so it'\''s all tracked, even cash.

### Acceptance criteria
- [ ] Date, amount, and source
- [ ] Shows on the dashboard right away

---
Epic: Club Treasury (Sprint 6, Jira CAR-21)'
make_issue 'US-52' 'Fix a mistake without erasing history' 'epic: Club Treasury' 'As an officer, I want to correct or void a wrong expense or deposit so a typo doesn'\''t wreck the books.

### Acceptance criteria
- [ ] Can edit an entry, or void it with a reason
- [ ] Voided entries stay visible but don'\''t count in totals
- [ ] The change records who made it and when

---
Epic: Club Treasury (Sprint 6, Jira CAR-21)'
make_issue 'US-53' 'See where the money stands' 'epic: Club Treasury' 'As an officer, I want a dashboard with the running balance, total in, and total out so I know where we are.

### Acceptance criteria
- [ ] Shows starting balance, deposits, expenses, current balance
- [ ] Lists recent activity

---
Epic: Club Treasury (Sprint 6, Jira CAR-21)'
make_issue 'US-54' 'Filter the money records' 'epic: Club Treasury' 'As an officer, I want to filter expenses and deposits by date range and type so I can find things.

### Acceptance criteria
- [ ] Date range filter
- [ ] Expense type filter

---
Epic: Club Treasury (Sprint 6, Jira CAR-21)'
make_issue 'US-55' 'Reconcile against the bank statement' 'epic: Club Treasury' 'As an officer, I want to compare the official bank statement to what CCOS shows so I can catch differences.

### Acceptance criteria
- [ ] Enter period and statement ending balance
- [ ] System shows what it calculated and the difference
- [ ] Notes field to explain a known difference

---
Epic: Club Treasury (Sprint 6, Jira CAR-21)'
make_issue 'US-56' 'See past reconciliations' 'epic: Club Treasury' 'As an officer or advisor, I want a history of reconciliations so there'\''s a paper trail.

### Acceptance criteria
- [ ] List of past periods with balances, difference, notes, and who did it

---
Epic: Club Treasury (Sprint 6, Jira CAR-21)'
make_issue 'US-57' 'Export a treasury report' 'epic: Club Treasury' 'As an officer, I want to export an HTML report for a date range so I can hand it to Troy or print it.

### Acceptance criteria
- [ ] Starting balance, deposits, expenses, ending balance
- [ ] Opens in any browser and prints cleanly

---
Epic: Club Treasury (Sprint 6, Jira CAR-21)'
make_issue 'US-58' 'Advisor has full treasury access' 'epic: Club Treasury' 'As the advisor, I want the same treasury access as officers so I can step in or check the books.

### Acceptance criteria
- [ ] Advisor can view and enter everything officers can

---
Epic: Club Treasury (Sprint 6, Jira CAR-21)'
make_issue 'US-59' 'Add deposits from ticket/order sales' 'epic: Club Treasury' 'As an officer, I want an easy way to log deposits that came from orders or event tickets so the treasury matches sales.

### Acceptance criteria
- [ ] Can start a deposit from a fulfilled order or an event'\''s sales
- [ ] Still lets me log deposits that didn'\''t come from the app

---
Epic: Club Treasury (Sprint 6, Jira CAR-21)'
make_issue 'US-60' 'Locked doors stay locked' 'epic: Hardening, Polish & Launch' 'As a club stakeholder, I want every officer/advisor-only screen to reject people who shouldn'\''t be there so club data and money are protected.

### Acceptance criteria
- [ ] Each protected page tested logged-out, as a member, as an officer, and as the advisor

---
Epic: Hardening, Polish & Launch (Sprint 7, Jira CAR-22)'
make_issue 'US-61' 'Works on a phone' 'epic: Hardening, Polish & Launch' 'As a member, I want every main page to work on my phone so I don'\''t need a laptop.

### Acceptance criteria
- [ ] No sideways scrolling, text is readable, buttons are tappable

---
Epic: Hardening, Polish & Launch (Sprint 7, Jira CAR-22)'
make_issue 'US-62' 'Helpful error pages' 'epic: Hardening, Polish & Launch' 'As any user, I want a clear page when something breaks or I land somewhere that doesn'\''t exist so I'\''m not stuck.

### Acceptance criteria
- [ ] Friendly 404 and error pages with a way back home

---
Epic: Hardening, Polish & Launch (Sprint 7, Jira CAR-22)'
make_issue 'US-63' 'Consistent look' 'epic: Hardening, Polish & Launch' 'As any user, I want the whole app to look like one product with the Cyber Cougars branding so it feels legit.

### Acceptance criteria
- [ ] Shared header with the club crest, same colors and fonts everywhere

---
Epic: Hardening, Polish & Launch (Sprint 7, Jira CAR-22)'
make_issue 'US-64' 'It'\''s live for Troy' 'epic: Hardening, Polish & Launch' 'As the advisor, I want the app running on a real address with my login working so the club can actually use it.

### Acceptance criteria
- [ ] Hosted and reachable over HTTPS
- [ ] Advisor account set up and working

---
Epic: Hardening, Polish & Launch (Sprint 7, Jira CAR-22)'
make_issue 'US-65' 'A guide for the next officers' 'epic: Hardening, Polish & Launch' 'As an incoming officer, I want a short how-to for each screen so I can pick this up without the original team.

### Acceptance criteria
- [ ] One-page guide per role (member, officer, advisor)
- [ ] Covers handing off the Treasurer job

---
Epic: Hardening, Polish & Launch (Sprint 7, Jira CAR-22)'

# ---------- open questions ----------
if echo "$EXISTING" | grep -q "^OPEN-QUESTIONS:"; then
  echo "skip  OPEN-QUESTIONS (already exists)"
elif [ "$MODE" = "go" ]; then
  gh issue create --repo "$REPO" --title 'OPEN-QUESTIONS: Decisions needed before grooming' --body 'Decide these as a team before grooming:

- [ ] **Payment** — does CCOS take real payment, or is it just "reserve now, pay in person"? (Affects US-30, US-39, US-59.)
- [ ] **Guest checkout** — must people log in to order, or can guests order with just an email? (US-30, US-39.)
- [ ] **Treasury fixes** — void-with-reason (my suggestion) vs. real delete. (US-52.)
- [ ] **Member removal** — hard remove, or just let dues lapse? (US-17.)
- [ ] **Order fulfillment** — does the club hand things out at meetings, and does that need a pickup date? (US-35.)
- [ ] **Emails** — should the app send any (order confirmations, dues reminders), or stay email-free for v1?' --label question >/dev/null && echo 'made  OPEN-QUESTIONS'
else
  echo 'would make  OPEN-QUESTIONS'
fi

echo
echo "Done. created/would-create: $CREATED   skipped: $SKIPPED"
