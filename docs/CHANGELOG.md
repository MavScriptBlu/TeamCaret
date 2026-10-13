# Changelog

Database and design decisions, newest first.

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
