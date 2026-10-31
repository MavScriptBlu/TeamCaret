# Changelog

Database and design decisions, newest first.

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
