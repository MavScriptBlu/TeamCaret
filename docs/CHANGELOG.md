# Changelog

Database and design decisions, newest first.

## 2026-11-09 · Mock sample venues and events

- The sample venues now have full addresses, so Edit venue and Restore venue work on them (the old "Sample Venue, Room 101" venues had no address, so Restore sent you to Edit first). One venue, Old Gym, starts out removed so Restore venue can be tried right away.
- The sample events have realistic names and descriptions, and cover the cases QA needs: four upcoming events, one members-only event with 12 tickets already sold (for the capacity check on Edit), one past event (hidden from /Events, still on the officer list), and one removed event (for Restore event).
- Sample data is only added to an empty database, so run `docker compose down -v` and then `docker compose up --build -d` to get it. That erases your local database, including anything you added while testing. The sample logins come back too, as long as `SAMPLE_ACCOUNT_PASSWORD` is set in `.env`.

## 2026-11-09 · CAR-69 follow-up: officer locks on events and venues, event member prices, past events

Found in the Sprint 4 QA run and the team's CAR-69 review, now that events and venues (#175) are in Dev.

- Officer lock on the event and venue officer endpoints: `GET /api/events/officer`, `GET /api/events/officer/{id}`, `POST`, `PUT` and `DELETE /api/events`, and `GET /api/venues/officer`, `GET /api/venues/{id}`, `PUT` and `DELETE /api/venues/{id}`. Not signed in answers 401, signed in but not an officer answers 403. Before this, a visitor who wasn't signed in could remove an event from the public event page. `GET /api/venues/{id}` is locked too, since only the officer pages use it and it includes removed venues.
- The officer menus on `/Events` and on an event's page (Create, Manage, Edit, Remove) only show for officers, like the nav and footer.
- Review fix: `GET /api/events/{id}` only returns upcoming events, the same rule as the list (`EventRepository.FindUpcomingAsync`, was `FindActiveAsync`). A past event can't be opened by its link; officers still reach it through `/api/events/officer/{id}`.
- Review fix: event ticket member prices follow the product rule. `TicketTypeDto.MemberPrice` is now `decimal?` and is null on `GET /api/events` and `/api/events/{id}` unless the caller is a current member; the officer endpoints always include it. The event page shows "Member price: log in as a member to see it" instead.
- Tests: the event API tests sign in as an officer (`TestAccounts.SignInAsOfficerAsync`). New tests: the event officer endpoints refuse a visitor who isn't signed in, visitors get no ticket member prices while officers do, and a past event's page is 404 while officers can still open it.

## 2026-11-08 · CAR-68 and CAR-69 Codacy: return addresses stay strings

- Codacy (SonarC# S3994, S3995, S3996) asked for `System.Uri` instead of `string` on `ReturnUrl` (Login, Register, AccessDenied), `AccountSession.SafeReturnUrl` and two test helpers. Kept as strings on purpose and marked with `[SuppressMessage]` plus the reason: it's a relative path from the query string, ASP.NET Core's own `LocalRedirect` and `Url.IsLocalUrl` take strings, and open redirects are already blocked by `SafeReturnUrl` (with tests). No change to how anything works.
## 2026-11-08 · CAR-60 Codacy fixes

Code-quality notes Codacy left on CAR-60. No change to what people see.

- `StatusModel`: the heading and message are worked out from the status code instead of being set to empty and overwritten. Status codes other than 404 and 503 are logged, and the page is never cached (`[ResponseCache(NoStore = true)]`), so a "not reachable" page doesn't stick around once the API is back.
- `ApiUnavailablePageFilter`: logs through a `[LoggerMessage]` method, with a fallback when the page has no display name. `IsApiUnavailable` is a plain `switch`, so it reads more easily.
- `ApiUnavailableTests`: the test data is a property, the site with the API switched off is started once and shared (`IClassFixture<ApiDownSite>`), and the three page checks are one test with three cases instead of three copies.

## 2026-11-08 · Review fixes for CAR-67, CAR-68 and CAR-69 (login)

From the team's review of the login stories:

- Creating an account with an email that's already used now says "An account with that email already exists." on the Email field, once. Before, it showed Identity's "Username '...' is already taken" (`AccountController.EmailTakenMessage`).
- Tests: `/api/me` for a current member (profile, Active status, member yes, officer no, no title, not the advisor).
- Tests: `AccessRulesTests` now also covers an officer whose dues expired and the advisor, through real web requests. An expired officer is refused on officer endpoints and gets no member prices. The advisor can use officer endpoints but gets no member prices, since the advisor has no membership. Expired members are refused on officer endpoints too.
- Still to do, after PR #175 (events and venues) merges, since it changes the same files: `GET /api/events/{id}` should only return upcoming events, and event ticket member prices should only go to current members, like products.

## 2026-11-05 · CAR-60 Tell people when the API can't be reached

- Most pages already showed their own message when the API was down (merch, search, events, the officer product pages, venue and account forms). This closes the gaps that were left.
- A product's page (`/Products/Details/{id}`) used to answer a blank browser error when the API was down or the product didn't exist. `UseStatusCodePagesWithReExecute("/Status/{0}")` now shows `Pages/Status.cshtml`: "The club's system isn't reachable right now" for 503, "We couldn't find that page" for 404, and a general message for anything else. The real status code is kept.
- Safety net: `ApiUnavailablePageFilter` (a global page filter) catches an API failure that a page didn't handle (can't connect, timed out, unreadable reply) and answers 503 with the same status page instead of crashing. A visitor closing the page isn't counted. Pages that show their own message keep doing so.
- The error page (`/Error`, outside Development) no longer shows the Microsoft template text; it says something went wrong and to try again, with a reference number.
- Messages stay on the page rather than in pop-up toasts, the same as the rest of the site.
- Not changed: the Create and Edit Event pages leave the venue dropdown empty when venues can't be loaded. Those pages are being rewritten in PR #175, so that's a note for that PR.

## 2026-11-04 · CAR-69 Lock officer pages and member pricing

- API: the officer product list (`?includeInactive=true`) and product create, update, reactivate and remove need the `Officer` policy, and so does adding a venue. Not signed in answers 401; signed in but not an officer answers 403. The event create and update endpoints don't exist yet (CAR-54, CAR-57); `EventsController` has a note to give them the same `[Authorize(Policy = ClubPolicies.Officer)]`.
- API: member prices on `GET /api/products` and `/api/products/{id}` only go to callers who pass the `Member` policy, which checks the dues date (`MemberExpiration`) on the club's date on every request. Guests, expired members and visitors who aren't signed in get `memberPrice: null`. Officers are current members, so they see them; the advisor (no membership) doesn't.
- Website: `AuthorizeFolder("/Officer", "Officer")` locks every officer page. A visitor who isn't signed in goes to Log in with a "Log in first to see that page." note; a signed-in person who isn't an officer sees the new "Officers only" screen (`/Account/AccessDenied`).
- Website: the sign-in cookie now notes whether the person is a current member and an officer (an advisor counts as an officer), read from `/api/me` at sign-in and again each time the token is renewed (about hourly). It only decides which pages open and which links show; the API's database check is the real guard.
- The officer links in the menu and footer (Product dashboard, Create event, Add venue) only show for officers.
- Public lists were already limited to active products and active, upcoming events, so no change there. Ticket prices on events are unchanged: members-only events show the member price as their price, and checking membership when tickets are bought belongs with ordering.
- The "lock this down with [Authorize]" and "restrict to officers" TODO comments are gone.

## 2026-11-04 · CAR-68 Log in, create account and log out pages

- New pages in `CCOS.Web`: `/Account/Login`, `/Account/Register` and `/Account/Logout`. A wrong email or password shows one message ("Email or password is incorrect.") so the page never says which emails have accounts. Create account shows the API's messages (email already used, weak password) next to the right field, then signs the person in right away.
- The website signs people in with a cookie (`AddAuthentication().AddCookie`). The API's sign-in tokens from `/api/auth/login` are kept inside that encrypted cookie, never in the browser's JavaScript. `AccountSession.ValidateAsync` renews the token through `/api/auth/refresh` a minute before it runs out, and signs the person out if it can't.
- `ApiBearerTokenHandler` adds the signed-in person's token to every call from the Products, Events and Venues clients, so CAR-69 can lock API endpoints without changing the pages. `AccountApiClient` has no handler, since it runs while signing in.
- The header's placeholder account button is now "Log in", or "Hi, {first name}" and a "Log out" button. Log out is a POST with the anti-forgery token, so a link on another site can't sign people out.
- After signing in, people go back to the page they came from. Only addresses on this site are allowed (`AccountSession.SafeReturnUrl`), so a link can't send them to another site.
- Nothing is locked yet: the "Log in first" and "Officers only" screens and locking officer pages are CAR-69.

## 2026-11-04 · Codacy: explicit status handling in the event update

- `EventsController.Update`: the switch on the update result now handles `Saved` on purpose, and the `default` throws `InvalidOperationException` so a new status can't be silently treated as saved. No behavior change today.
- Codacy also flagged the missing `[ValidateAntiForgeryToken]` on `POST /api/events`. Not changed on purpose: the events API is stateless JSON with no cookie login, so a CSRF token has nothing to protect. Revisit when login (CAR-1) lands.
- Codacy also flagged the officer endpoints for having no authorization. Still open on purpose: there is no login yet, and each officer endpoint carries a `TODO` pointing at CAR-1.

## 2026-11-04 · Performance: batched ticket updates and CancellationToken in the sample data

- `TicketBookingRepository`: reserving seats for an order used to run one `UPDATE` per event, and canceling an order ran two per event (the N+1 pattern). Reserving is now one `UPDATE` for the whole order. Canceling is one `UPDATE` for the registrations plus one for the events.
- Same promises as before: an event that is full, removed or missing means the whole reserve changes nothing and throws `SoldOutException` (now naming the first such event), the database still re-checks the room on each row, and `CK_Events_TicketsSold` is the last safety net. Canceling twice is still harmless.
- Tradeoff: the old loop took events one at a time in event id order so two orders could not lock each other out. One statement lets the database pick the lock order, so its deadlock detection protects us now. If it ever picks an order as the loser, that order fails and can be retried.
- The batched cancel stops (and rolls back) with `InvalidOperationException` if only some of an order's tickets were still purchased, because it can no longer tell how many seats each event gets back. A plain double cancel still returns 0.
- The `UPDATE` text only holds `{0}`, `{1}` slots; the ids and amounts go in as real parameters. It uses SQL that SQLite and PostgreSQL both understand. The 13 new tests (`TicketBookingBatchTests`) run on SQLite. I also ran them once against a local PostgreSQL 16 and they passed. Not run on PostgreSQL 17 yet.
- `DevelopmentData.EnsureCreatedAndSeededAsync` and its seed methods take a `CancellationToken`, and `Program.cs` passes the app's stopping token, so shutting down during startup stops the sample-data setup early.

## 2026-11-04 · CAR-55 and CAR-56 Events save, and event Remove / Restore

- New API calls: `POST /api/events`, `PUT /api/events/{id}`, `DELETE /api/events/{id}`, plus `GET /api/events/officer` and `GET /api/events/officer/{id}` so removed and past events can be listed and opened. Creating an event also creates its "General" ticket type. Updating changes the prices on the first ticket type still on sale.
- Shared rules in `EventRules` (end after start, member price not above ticket price). A capacity below the tickets already sold is refused, a removed venue can't be picked for a new event, and a save that loses a race with another officer gets a 409 (the `xmin` check).
- Create Event and Edit Event now save. The form checks every field and shows the message under the field. Club time is converted to UTC with `ClubTime.ToUtc`; a time that doesn't exist when the clocks spring forward is refused with a message.
- Remove wording (follows CAR-65): the event page menu says **Remove event** and opens the shared "Remove?" dialog with the soft-delete disclaimer (nothing is erased, the event is only hidden and can be restored). The old "Cancel event" dialog, which did nothing, is gone. Edit Event has a **Show in the event list** box (untick removes, tick restores).
- New officer list at `/Officer/Events` ("Manage events"): filter **In the event list** / **Removed**, **Remove event** and **Restore event** buttons, linked from the menu on `/Events` and the nav.
- Not changed: the Category box is still sample data and isn't saved (no category column yet).
- Tests: 23 new API tests and 41 new website tests (the form rules, UTC conversion including summer time, API error messages on the right fields, remove and restore).

## 2026-11-04 · CAR-58 Venue pages: list, edit, Remove / Restore

- New pages under `/Officer/Venues`: a list of every venue (removed ones too) with a filter, **Remove venue** with the shared "Remove?" dialog and soft-delete disclaimer, and **Restore venue**. A venue with no address (the sample venues) is sent to Edit to add one before it can be restored.
- New **Edit Venue** page with the same form as Create, plus the **Show in the venue list** box. The form is now shared (`VenueFormPageModel` and `_VenueForm`), so Create and Edit say the same things.
- The Remove dialog and its script are generic (`_RemoveDialog`, `officer-remove-dialog.js`), so events use them too. The dialog adds a line saying nothing is permanently erased.
- "Manage venues" links added to the nav, the footer and the menu on `/Events`.

## 2026-11-04 · CAR-57 Venue endpoints and CAR-63 ZIP code rule

- CAR-57: `GET /api/venues/officer`, `GET /api/venues/{id}`, `PUT /api/venues/{id}` and `DELETE /api/venues/{id}` (a soft delete: `IsActive = false`). "Officer list" is read as the list officers use: every venue including removed ones, with its address.
- `PUT` with `isActive: true` restores a removed venue. The unique-name check skips the venue being edited, so saving a venue under its own name works.
- CAR-63: ZIP codes must be 5 digits or ZIP+4 (`^\d{5}(-\d{4})?$`) on the API (`CreateVenueRequest`, `UpdateVenueRequest`) and on the Razor forms. One rule in `CCOS.Shared.ZipCodeRules` feeds both, so the messages match.
- Tests: ZIP rule (5-digit, ZIP+4, invalid), venue GET, list, PUT and DELETE.

## 2026-10-30 · CAR-67 Login API and access policies

- `CCOS.Api` signs people in with ASP.NET Core Identity: `AddIdentityApiEndpoints<AppUser>` plus `MapIdentityApi` under `/api/auth` (login, refresh, forgot and reset password). One account per email (`RequireUniqueEmail`). Not signed in answers 401 and not allowed answers 403, never a redirect.
- `POST /api/account/register` creates the Customer and the login in one transaction (`AccountRepository.RegisterAsync`), so a failed login (email taken, weak password) rolls the customer back too. Identity's built-in `/api/auth/register` is turned off with a 404, because it can't create the Customer every login needs. This is the one change from the CAR-52 plan, which listed register under `MapIdentityApi`.
- `GET /api/me` returns who's signed in (`CurrentAccountDto`): customer id, names, email, member status, current member, current officer and officer title, worked out fresh on the club's date.
- Two policies, `Member` and `Officer` (`ClubPolicies` in `CCOS.Shared`). `MembershipAuthorizationHandler` finds the CustomerId behind the signed-in login and asks `MemberRepository` on every request, so renewals, expirations and leaving the club count right away. An officer whose dues expired fails both until they renew.
- Advisor (from the Remaining Work checklist added to CAR-67): Identity roles are on (`AddRoles<IdentityRole>`), and the club's advisor (`ClubRoles.Advisor`, Troy) passes the `Officer` policy without a membership or title. The role is read from the database on every request, like membership. `/api/me` has `isAdvisor`. The roles tables already came with CAR-66, so no migration. `DevelopmentData` seeds `advisor@ccos.test`, checked on its own so an already-seeded database gets it too. This is a simple stand-in for the admin; the Admins table is still CAR-74.
- Nothing is locked yet: applying the policies to the officer endpoints and member prices is CAR-69.
- Tests: `AccountApiTests` covers register and login, `/api/me` for a guest and an officer, 401 without signing in, a duplicate email (400, no leftover customer), the turned-off built-in register, a wrong password, the Member and Officer rules for guest, member, expired member, officer and expired officer, the advisor passing Officer (and losing it when the role is removed), and a membership change showing up on the next request. The test password is random each run, so no credential is in the code.
- `docs/api/README.md`: the Auth section now describes the real endpoints and policies.

## 2026-10-30 · CAR-65 "Remove" instead of "Delete" / "Deactivate"

- Instructor feedback from the Sprint 3 review: nothing is ever deleted, so the site says "Remove" and "Restore". On the officer product dashboard: the **Remove product** button, the **Removed** badge, "Removed from the store. Restore it from Edit.", the **Remove product?** dialog ("Yes, remove", and it now says nothing is deleted), and the message "Product removed. You can restore it from Edit." Errors say "removed" too.
- The dashboard filter reads **In the store** / **Removed** (was Active / Inactive), and the product facts show "In the store: Yes / No, removed" instead of `IsActive`.
- The Edit page's Active box is now **Show in the store**, with a hint: untick to remove, tick to restore.
- Code names didn't change (the `Delete` page handler, `IsActive`, `DELETE /api/products/{id}`, CSS classes, file names), so the API and tests are untouched. Log messages say "remove" to match.
- Docs: the demo script (CAR-42) and the product QA checklist (CAR-15) use the new wording. Event and venue remove pages (CAR-56, CAR-58) should use the same words.

## 2026-10-30 · CAR-72 API tests run in test mode · CAR-73 Dependabot targets Dev

- CAR-72: `ProductsControllerTests` now turns on test mode with `builder.UseSetting("Testing", "true")`, the setting `Program.cs` actually reads. It used to set a `CCOS_TEST_MODE` environment variable that nothing read, so on a machine with a PostgreSQL user secret the API registered PostgreSQL next to the test's SQLite database and all 5 API tests failed. CI passed only because it has no user secret. The unused variable is gone.
- CAR-73: `.github/dependabot.yml` sets `target-branch: "Dev"` for NuGet and GitHub Actions, so dependency update PRs go into Dev instead of main. Dependabot reads this file from main, so it takes effect after the next Dev → main sync.
- Team rule (from the Sprint 3 retro): Dev is merged into main once at the end of each sprint, after the review.

## 2026-10-28 · CAR-66 Membership and login tables

- New tables from the CAR-28 design: `Customers`, `Members`, `Officers` and `OfficerHistory`. `MemberId` is the `CustomerId` and `OfficerId` is the `MemberId` (not generated). Membership dates are `DateOnly` (`date` in PostgreSQL). Status isn't stored: `Member.StatusOn(today)` works it out from `IsActive` and `MemberExpiration`, and `MemberStatus` lives in `CCOS.Shared`.
- `AppDbContext` is now an `IdentityDbContext<AppUser>`, so ASP.NET Core Identity's tables (`AspNetUsers` and the rest) are in the same database and migrations (CAR-52). `AppUser.CustomerId` is required and unique: one login per customer. Added `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.12 to `CCOS.Data`, and `CCOS.Data` now references `CCOS.Shared`.
- Every relationship is Restrict (no cascade deletes), including Identity's own, which default to cascade. Members have no query filter. `OfficerHistory` has a filtered unique index (`UX_OfficerHistory_MemberId_Open`), so a member can't have two open titles.
- **Admins deferred:** the Admins table isn't built yet (CAR-74). `Members.DeactivatedByAdminId`, `Officers.GrantedByAdminId` and `OfficerHistory.GrantedByAdminId` exist as optional columns with no link to another table. This is different from the ERD and CAR-28 until CAR-74 is done.
- `CCOS.Repositories`: `ClubClock` gives the club's date (America/Chicago), and `MemberRepository` answers "current member?" and "current officer?" with that date passed in. An officer must also be a current member.
- `DevelopmentData` seeds three sample logins (officer, member, guest) when there are no customers. Their shared password is never in the code: it's read from the `SampleAccounts:Password` setting (user secrets for `dotnet run`, `SAMPLE_ACCOUNT_PASSWORD` in `.env` for Docker). If it isn't set, the sample logins are skipped with a warning. Codacy flagged the first version, which had the password in the code. Setup is in `backend/README.md`.
- Tests: `MembershipSchemaTests` covers the model rules, `date` columns on PostgreSQL, member status, the club time zone, the current-member and current-officer rules, and the one-open-title index.
- If you use the SQLite fallback, delete `backend/CCOS.Api/ccos-dev.db` once so it rebuilds with the new tables.

## 2026-10-26 · Officer product Save button works after a validation error

- Checked the officer product create, edit and delete pages end to end against `CCOS.Api` in a browser: create, edit, deactivate from the dashboard, reactivate with the Active box on Edit, and the "not found" page all work.
- Fixed: after the server showed a message (for example "member price cannot be greater than the regular price"), fixing the field and clicking Save did nothing. Clicking moved focus off the field, the validator cleared the message, the form got shorter and Save moved about 50px up before the click finished. `product-create.js` now keeps focus in place when Save is pressed with the mouse, so the click lands and the form submits. Used by both Create and Edit.
- Noted (not changed): the event form has the same layout and may have the same problem.

## 2026-10-19 · Code comments and spacing pass

- Added `///` summaries to every class, record and public method that didn't have one: the data layer (`AppDbContext`, entities, `UtcDateTimeConverter`), `TicketBookingRepository` and its helper types, the API (`Program.cs`, `ProductsController`, `DevelopmentData`), the shared request records, the website `Program.cs`, services, Razor page models (public pages and the officer product, event and venue pages) and the schema tests.
- Added short `//` notes above the non-obvious steps: why `TicketBookingRepository` sorts events before locking them, how the single conditional `UPDATE` stops overselling, why `Event.RowVersion` uses `xmin`, why products use a query filter, and the "try, catch, show a friendly message" blocks on the officer product pages.
- Added blank lines between logical blocks in the files above.
- Renamed a few short names to full words to match the no-abbreviations rule: `ev`, `e`, `t`, `v`, `c` and `p` in lambdas and parameters (`eventInfo`, `ticketType`, `venue`, `category`, `product`), and `products` to `product` in `Officer/Products/Edit.cshtml.cs` where it held a single product.
- No behavior changes. Only comments, blank lines and those renames.
- Docs: `docs/CODING_STANDARDS.md` now spells out the comment rules, and the README mentions them under Build, Test, and CI.
- Taskfile: every task now prints a short "starting" line and a "done" line, so a quiet command no longer looks like it did nothing. `task test` also lists each test as it runs.
- Replaced constant arrays passed to methods (`[nameof(MemberPrice)]`, the expected column lists in the schema tests) with `static readonly` fields, as the analyzer suggests (CA1861).
- Noted (not changed): `Class1.cs` in `CCOS.Shared` and the `/weatherforecast` sample in `CCOS.Api/Program.cs` are leftover template code that nothing uses.

## 2026-10-14 · CAR-45 Shared PostgreSQL 17 database with EF Core migrations

- `docker-compose.yml`: `ccos-db` is PostgreSQL 17 (`postgres:17`) on port 5432, with the named volume `ccos-data`. The password comes from `.env`, and `.env.example` (committed) has the shared dev password, so nobody has to invent one.
- `CCOS.Data`, `CCOS.Api` and `AppDbContextFactory` use the Npgsql provider (`Npgsql.EntityFrameworkCore.PostgreSQL`). EF Core is pinned to 10.0.12 in `CCOS.Data` so every project builds against the same version.
- `AppDbContext`: check constraints and query filters use PostgreSQL syntax, times are `timestamp(0) with time zone` with `now()` defaults.
- Event concurrency uses PostgreSQL's built-in `xmin` column (`uint RowVersion`, type `xid`) as the EF Core concurrency token. Ticket booking is still one conditional `UPDATE`, so CAR-30 overselling stays blocked.
- Migrations: the old set was replaced by one fresh `InitialPostgresSchema` migration. `dotnet ef database update` builds every table from nothing.
- `CCOS.Api` has a `UserSecretsId`. The API reads `ConnectionStrings:CCOS` from user secrets. With no connection string, Development still falls back to the SQLite sample file.
- `AppDbContextFactory` (used by `dotnet ef`) reads the same user secret, or the `CCOS_CONNECTION_STRING` environment variable in CI. It stops with a message that points to the setup steps if neither exists. Added `Microsoft.Extensions.Configuration.UserSecrets` to `CCOS.Data` for this.
- Added `.config/dotnet-tools.json` so `dotnet tool restore` installs the pinned `dotnet ef` (10.0.12).
- Tests: the schema test that checked SQL Server's `rowversion` now checks the PostgreSQL `xmin` setup. It only builds the model, so it needs no running database.
- Taskfile: new `task migrate`, `task up` and `task reset` check `POSTGRES_PASSWORD`, `task db` starts PostgreSQL.
- CI: waits for PostgreSQL with `pg_isready`, then applies every migration with `dotnet ef database update` before checking the API and website.
- The `ccos-api` container now uses PostgreSQL too (it was on its own SQLite file). `docker-compose.yml` hands it `ConnectionStrings__CCOS` pointing at `ccos-db`, built from the `.env` values, so the website, API and database containers all share one database. It also fixes the API container crash loop from the SQLite column type. Run `task migrate` once after the first `task up`.
- Ports written down in one place (README and `docs/database-setup.md`): website 8080, API 8081 (8080 inside the container), database 5432, each overridable with `WEB_PORT`, `API_PORT` or `POSTGRES_PORT` in `.env`.
- Docs: `backend/README.md` has the full setup steps (Docker Desktop or Podman, start the container, user secrets, migrations, run the API, fixing a password mismatch). `docs/database-setup.md` rewritten for PostgreSQL and DBeaver. README tech stack, database setup and CI sections, the proposal tech stack, the schema docs, the ERD and the soft delete docs now say PostgreSQL.

## 2026-10-13 · CAR-45 Run the API and website in containers

- Added `backend/CCOS.Api/Dockerfile` and `frontend/CCOS.Web/Dockerfile` (.NET 10, multi-stage build, runs as the non-root `app` user, listens on 8080 inside the container).
- `docker-compose.yml` now runs three containers: `ccos-db`, `ccos-api` (<http://localhost:8081>) and `ccos-web` (<http://localhost:8080>). The website reaches the API by its service name (`http://ccos-api:8080/`).
- Added `.dockerignore` so `.env`, `bin/`, `obj/` and local `.db` files never go into an image.
- Taskfile: `task up` now builds and starts the whole app, new `task db` starts only Postgres, `task logs` shows all three, `task reset` rebuilds too.
- CI: the database job is now "App starts". It runs `task up`, checks Postgres, then checks `/api/products` and the website's `/Products` page both answer.
- `.env.example`: optional `WEB_PORT` and `API_PORT` overrides.
- Fixed two formatting nits `task lint` caught (a double space in the API's `Program.cs`, using order in `Pages/Index.cshtml.cs`).
- Note: the API still uses its own SQLite file with sample data, not Postgres. Switching it over is separate database work.
- README: new Run the Whole App in Containers section, tech stack now says Razor Pages, structure tree updated. `docs/database-setup.md`: DB-only command and Task table updated.

## 2026-10-13 · CAR-45 Code style, linting, and Dependabot

- Added `.editorconfig` with the team's code style (4-space indents, file-scoped namespaces, `var`, braces, `_camelCase` private fields). Style rules are suggestions, so they never break the build. EF migrations are skipped, and test names can use underscores.
- Added `Directory.Build.props` so every project runs Microsoft's recommended code checks (analyzers). They show up as build warnings, not errors.
- Taskfile: added `task format` (fixes formatting) and `task lint` (checks it without changing files).
- CI: added a Check formatting step that runs `task lint` between build and test.
- Added `.github/dependabot.yml`: weekly update PRs for NuGet packages (Microsoft packages grouped into one PR) and GitHub Actions.
- README: Build, Test, and CI section now covers code style, testing tools (xUnit + `Microsoft.AspNetCore.Mvc.Testing`, since the frontend is Razor Pages), and Dependabot; structure tree updated.
- `docs/database-setup.md`: Task table mentions format and lint.

## 2026-10-13 · CAR-45 Podman support and Task shortcuts

- Added `Taskfile.yml` with `task up`, `task down`, `task logs`, and `task reset`. Uses Docker by default; set `CONTAINER=podman` in `.env` to use Podman.
- Taskfile: `task up` and `task reset` now stop with a clear message if `.env` is missing or `POSTGRES_PASSWORD` is blank; `up` and `reset` have `task --summary` help text.
- `.env.example`: added a commented-out `CONTAINER=podman` line.
- Added `task build` and `task test`, plus a GitHub Actions workflow (`.github/workflows/ci.yml`) that installs Task and runs build, test, and a database start/stop check on every push and PR.
- `docker-compose.yml` now uses the full image name `docker.io/library/postgres:16` so Podman pulls it without asking which registry. Docker works the same as before.
- `docs/database-setup.md`: added a Using Podman section (machine setup, `podman compose`, optional `docker` alias, gotchas).
- README: Local Database Setup and tech stack now mention Podman and the `task` shortcuts; new Build, Test, and CI section; `Taskfile.yml` and the CI workflow added to the structure tree.
- `docs/database-setup.md`: added a Shortcuts with Task section.

## 2026-10-12 · CAR-45 Local Postgres setup

- Added `docker-compose.yml` to run PostgreSQL 16 locally in Docker. Settings come from a `.env` file.
- Added `.env.example` as the template; `.gitignore` now lets it through while still ignoring the real `.env`.
- Added `docs/database-setup.md` with setup steps, DBeaver connection info, troubleshooting, and Postgres vs SQLite notes for the team.
- README: added a Local Database Setup section and updated the tech stack.

## 2026-10-05 · Schema sync

- ERD: added the CAR-30 event and ticket changes (`TICKET_REGISTRATION`, `Events` start/end times, `TicketsSold`, `IsActive`, `RowVersion`, `Venues.IsActive`, `Products.ProductType`), CAR-24's `OrderLines.ProductNameSnapshot`, and the `ADMIN` → `MEMBER` link from CAR-28. The ERD now renders as a diagram on GitHub.
- Schema doc: replaced the outdated hand-written SQL with pointers to the migrations, the ERD and the design docs.
- Lifecycle audit doc: updated the Member, Officer, Venue, Event and Ticket rows to match CAR-28 and CAR-30.

## 2026-10-04 · CAR-30 Event, ticket and venue schema

- Tickets are Products with `ProductType = 'Ticket'`; each admission is a `TicketRegistrations` row.
- `Events.TicketsSold` with a `CHECK` against `TicketCapacity` blocks overselling in the database.

## 2026-10-01 · CAR-28 Membership lifecycle

- Member status (Active/Expired/Inactive) is worked out from `IsActive` + `MemberExpiration`; `IsCurrent` removed.
- Member rows are never deleted; foreign keys into `Customers` and `Members` are `NO ACTION`.

## 2026-09-25 · CAR-24 Product soft delete

- "Delete" archives a product (`IsActive = 0`); officers can't hard-delete.
- Global query filter hides archived products; order history reads snapshots from `OrderLines`.
