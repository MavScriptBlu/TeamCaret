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

- ASP.NET Core (Web API backend + Razor Pages frontend)
- ASP.NET Core (Web API backend + Razor Pages frontend)
- Entity Framework Core
- PostgreSQL 17 in a container for local development (Docker or Podman), via the Npgsql EF Core provider
- SQLite sample file as a fallback when you run the API with no connection string
- Docker/Podman containers for the API and website too (`task up` runs all three)
- PostgreSQL 17 in a container for local development (Docker or Podman), via the Npgsql EF Core provider
- SQLite sample file as a fallback when you run the API with no connection string
- Docker/Podman containers for the API and website too (`task up` runs all three)
- ASP.NET Core Identity for authentication
- Deployed to Azure App Service

## Local Database Setup

1. Copy `.env.example` and rename the copy to `.env`. The shared dev settings in it work
   as they are, so you don't have to type anything. `.env` is gitignored, so never commit it.
2. Start Docker Desktop, then from the repo root run:

   ```bash
   docker compose up -d ccos-db
   ```

   Using Podman instead? Run `podman machine start`, then `podman compose up -d ccos-db`.
   Same file, same result. Details in `docs/database-setup.md`.

   Shortcut: if you have [Task](https://taskfile.dev) installed, run `task db` to start
   just the database, or `task up` for the whole app (see below). `task down` stops it all.
   Run `task` to see every shortcut.

3. Put the connection string in your user secrets and create the tables with `task migrate`.
   Then `dotnet run` the API and it uses PostgreSQL. Exact commands are in `backend/README.md`.
4. Want to look at the tables? Connect DBeaver to `localhost:5432`, user `ccos-db`,
   and the password from your `.env`.

Full steps and troubleshooting are in `docs/database-setup.md`.

## Run the Whole App in Containers

With your `.env` set up (step 1 above) and Docker or Podman running:

```bash
task up      # builds and starts the database, API, and website
task down    # stops everything (database data stays)
task logs    # watch all three (Ctrl+C to quit)
```

No Task? `docker compose up -d --build` does the same thing.

### Ports: what's where

| Piece    | Container  | Open it on your computer                  | Port inside Docker | Change it with  |
| -------- | ---------- | ----------------------------------------- | ------------------ | --------------- |
| Website  | `ccos-web` | `http://localhost:8080`                   | 8080               | `WEB_PORT`      |
| API      | `ccos-api` | `http://localhost:8081/api/products`      | 8080               | `API_PORT`      |
| Database | `ccos-db`  | `localhost:5432` (DBeaver, `dotnet ef`)   | 5432               | `POSTGRES_PORT` |

- Set the "Change it with" names in your `.env` only if something else already uses the port.
- Containers talk to each other by name, not `localhost`. The website reaches the API at `http://ccos-api:8080/`.
- Running the API yourself with `dotnet run`? It listens on `http://localhost:5173` instead.

The first `task up` takes a few minutes because it downloads .NET. After that it's quick.
Each project has its own `Dockerfile` (`backend/CCOS.Api/Dockerfile`, `frontend/CCOS.Web/Dockerfile`),
and `.dockerignore` keeps your `.env` and build junk out of the images.

All three containers work together: the website calls the API container, and the API container
uses the PostgreSQL container (same user, password and database as your `.env`). The tables are not
there on a brand new database, so run `task migrate` once after your first `task up` (see
`backend/README.md`). Port taken? Set `WEB_PORT`, `API_PORT` or `POSTGRES_PORT` in your `.env`.

## Build, Test, and CI

With [Task](https://taskfile.dev) installed:

```bash
task build   # dotnet build CCOS.slnx
task test    # dotnet test CCOS.slnx
task format  # fixes spacing and indents to match .editorconfig
task lint    # checks formatting without changing anything (CI runs this)
```

Every task prints a "starting" line and a "Done." line, so you can tell it finished. If something
fails, the red error from Task or dotnet shows up instead of "Done.".

**Code style:** `.editorconfig` holds the team's style rules, and Visual Studio, VS Code and
Rider read it automatically. `Directory.Build.props` turns on Microsoft's built-in code checks
(analyzers) for every project. They show up as warnings when you build, never errors.
If CI says your formatting is off, run `task format` and commit what it changed.

**Code comments:** every class and public method has a `///` summary that says what it is for,
and tricky steps have a short `//` note above them. Blank lines split the code into logical blocks.
Names are spelled out in full (`item`, not `i`). Keep this up when you add code
(details in `docs/CODING_STANDARDS.md`).

**Testing:** xUnit for tests. The frontend is Razor Pages (`.cshtml`), so page tests use
`Microsoft.AspNetCore.Mvc.Testing`, which runs the site in memory and lets a test load a page.

GitHub Actions (`.github/workflows/ci.yml`) runs on every push and pull request. It runs
`task build`, `task lint` and `task test`, and it also runs `task up`, waits for PostgreSQL, applies
every migration with `dotnet ef database update`, checks that the API and website answer, then stops
them with `task down`. If the CI check goes red on a PR, click it to see which step failed.

Dependabot (`.github/dependabot.yml`) opens a PR once a week when a NuGet package or
GitHub Action has an update. CI runs on it, so merge it if it's green.

## Project Structure

```text
TeamCaret/
├── backend/              ASP.NET Core Web API + EF Core (Web API, repository, data layers)
├── frontend/             Razor Pages app that calls the backend API (view layer)
├── shared/               DTOs/enums shared between backend and frontend (the API contract)
├── docs/                 Proposal, ERD, schema, backlog, changelog
│
├── .github/
│   ├── workflows/
│   │   └── ci.yml        GitHub Actions: build, lint, test, and app start check
│   └── dependabot.yml    Weekly update PRs for NuGet packages and Actions
│
├── .config/
│   └── dotnet-tools.json Pins the `dotnet ef` tool version (`dotnet tool restore` installs it)
│
├── docker-compose.yml    Local PostgreSQL, API, and website containers (reads .env)
├── Taskfile.yml          Shortcuts: `task up`, `task down`, `task build`, `task test`, `task lint`
├── Directory.Build.props Turns on .NET code checks for every project
├── .editorconfig         Code style rules (spacing, naming) every editor reads
├── .dockerignore         Keeps .env and build output out of Docker images
├── .env.example          Template for your local .env (copy it, add your password)
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
