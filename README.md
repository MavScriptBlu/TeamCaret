# Cyber Cougars Club Order & Event System (CCOS)

Team Caret - Agile Team Semester Project (Software Architecture)

CCOS lets Cyber Cougars members (and non-member guests, for public events)
order club swag/resources and buy event tickets, while officers manage
products, venues, events, and membership dues. Built as an ASP.NET Core MVC +
Entity Framework Core application with a real database and role-based auth,
scaled up from the Order Entry System (OES) pattern used in Assignment 1.1.

See `docs/Project Proposal_ Cyber Cougars Club Order & Event System (CCOS).md`
for the full proposal, `docs/ERD_...md` for the entity-relationship diagram,
and `docs/CyberCougar OES Schema.md` for the SQL schema.

Feature-level design docs live alongside those, e.g.
`docs/CAR-1_Product_CRUD_Design.md` for the Product CRUD interface + database
design.

## Tech Stack

- ASP.NET Core MVC (Web API backend + Razor Pages/Blazor frontend - team's
  final call, see proposal)
- Entity Framework Core
- SQL Server (Azure SQL in production), under review: see `docs/database-setup.md`
- PostgreSQL 16 in a container for local development (Docker or Podman)
- ASP.NET Core Identity for authentication
- Deployed to Azure App Service

## Local Database Setup

1. Copy `.env.example`, rename the copy to `.env`, and put your own password in it.
   `.env` is gitignored, so never commit it.
2. Start Docker Desktop, then from the repo root run:

   ```bash
   docker compose up -d
   ```

   Using Podman instead? Run `podman machine start`, then `podman compose up -d`.
   Same file, same result. Details in `docs/database-setup.md`.

   Shortcut: if you have [Task](https://taskfile.dev) installed, just run `task up`
   to start and `task down` to stop. Run `task` to see every shortcut.

3. Connect with DBeaver (or any Postgres tool) using the values from your `.env`.

Full steps, troubleshooting, and Postgres vs SQLite notes are in
`docs/database-setup.md`.

## Build, Test, and CI

With [Task](https://taskfile.dev) installed:

```bash
task build   # dotnet build CCOS.slnx
task test    # dotnet test CCOS.slnx
```

GitHub Actions (`.github/workflows/ci.yml`) runs on every push and pull request. It runs
`task build` and `task test`, and it also checks that the database starts with `task up`
and stops with `task down`. If the CI check goes red on a PR, click it to see which step failed.

## Project Structure

```text
TeamCaret/
├── backend/        ASP.NET Core Web API + EF Core (Web API, repository, data layers)
├── frontend/        Razor Pages/Blazor app that calls the backend API (view layer)
├── shared/         DTOs/enums shared between backend and frontend (the API contract)
├── docs/           Proposal, ERD, schema, backlog, changelog
├── docker-compose.yml  Local Postgres database (reads settings from .env)
├── Taskfile.yml    Shortcuts: `task up`, `task down`, `task build`, `task test`, and more
├── .github/workflows/ci.yml  GitHub Actions: build, test, and database check
├── .env.example    Template for your local .env (copy it, add your password)
├── .gitignore
└── README.md
```

Each of `backend/`, `frontend/`, and `shared/` has its own README describing
what goes in it. None of the actual .NET projects exist yet - `dotnet new` them
into place per those READMEs when implementation starts.

## Team

- Blue Johnas
- Seng Lee
- Dylan Morzfeld
