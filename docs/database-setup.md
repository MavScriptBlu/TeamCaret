# CCOS database setup (PostgreSQL + Docker or Podman + DBeaver)

CCOS uses **PostgreSQL 17** running in a container (`postgres:17`, Npgsql provider for EF Core).
Everyone gets the same database engine on their own computer, and the EF Core migrations
are written for it.

The short version for coders (user secrets, migrations, running the API) is in
`backend/README.md`. This doc covers the container side, Podman, Task, and DBeaver.

## Get PostgreSQL running

1. Open Docker Desktop and make sure it says it's running. (On Podman? Skip to [Using Podman](#using-podman) below.)
2. In the repo root, copy `.env.example` and rename the copy to `.env`. (`.env` is gitignored, never commit it.)
3. The shared dev settings inside work as they are (`POSTGRES_PASSWORD=ccos-dev-password`). The password only protects a database on your own computer. Want your own? Change it BEFORE the first start. Changed it later? Run `task reset` (it wipes local data) or PostgreSQL keeps the old one.
4. Open a terminal in the repo root and run:
   ```
   docker compose up -d ccos-db
   ```
   (`ccos-db` starts just the database. Leave it off and you get the whole app, which `task up` does too.)
5. Check it's alive:
   ```
   docker ps
   ```
   You should see `ccos-db` with `0.0.0.0:5432->5432`.
6. Create the tables: `task migrate` (or the two commands in `backend/README.md`). You need the user secret from `backend/README.md` step 3 first.

Your data lives in the `ccos-data` volume, so it survives restarts and reboots.

## Using Podman

Podman runs the exact same `docker-compose.yml`. Nobody else on the team has to change anything.

1. One time only, make the Podman VM:
   ```
   podman machine init
   ```
2. Every time you boot up (this is the Podman version of "open Docker Desktop"):
   ```
   podman machine start
   ```
3. Do the `.env` steps from above (copy `.env.example` to `.env`).
4. From the repo root:
   ```
   podman compose up -d ccos-db
   podman ps
   ```
   You should see `ccos-db` with `0.0.0.0:5432->5432`.

`podman compose` hands the work to a compose tool behind the scenes. Podman Desktop sets that up for you. If you get "looking up compose provider failed", install one: `pip install podman-compose`.

### Want to keep typing `docker`?

Add this line to your PowerShell profile (run `notepad $PROFILE` to open it):

```powershell
Set-Alias docker podman
```

Open a new terminal and every `docker ...` command in this doc runs Podman instead. Only affects your machine.

### Podman gotchas

- **"Cannot connect to Podman"**: the VM is off. Run `podman machine start`.
- **Port 5432 not reachable from DBeaver**: run `podman machine stop` then `podman machine start`, and try again.
- All the fixes in "If it breaks" below work the same, just swap `docker` for `podman`.

## Shortcuts with Task

The repo has a `Taskfile.yml` so you don't have to remember the long commands.

Install Task once (Windows):

```
winget install Task.Task
```

Then from the repo root:

| Command        | What it does                                                    |
| -------------- | --------------------------------------------------------------- |
| `task db`      | Starts only the database and shows what's running              |
| `task migrate` | Creates or updates the tables (uses your user secret)          |
| `task up`      | Builds and starts the whole app (database, API, website)       |
| `task down`    | Stops everything (your data stays)                             |
| `task logs`    | Shows logs from all three (Ctrl+C to quit)                     |
| `task reset`   | WIPES the database and starts fresh (asks you first)           |
| `task`         | Lists all of these (plus build, test, format and lint)          |

Want the details on one? Run `task --summary up` (or `migrate`, `reset`). If your `.env` is missing or the password is blank, `task up` stops and tells you what to fix instead of starting a broken database.

Task uses Docker by default. On Podman, add `CONTAINER=podman` to your `.env`. (The PowerShell `docker` alias doesn't work inside Task, so use this instead.)

## Shortcuts with Task

The repo has a `Taskfile.yml` so you don't have to remember the long commands.

Install Task once (Windows):

```
winget install Task.Task
```

Then from the repo root:

| Command        | What it does                                                    |
| -------------- | --------------------------------------------------------------- |
| `task db`      | Starts only the database and shows what's running              |
| `task migrate` | Creates or updates the tables (uses your user secret)          |
| `task up`      | Builds and starts the whole app (database, API, website)       |
| `task down`    | Stops everything (your data stays)                             |
| `task logs`    | Shows logs from all three (Ctrl+C to quit)                     |
| `task reset`   | WIPES the database and starts fresh (asks you first)           |
| `task`         | Lists all of these (plus build, test, format and lint)          |

Want the details on one? Run `task --summary up` (or `migrate`, `reset`). If your `.env` is missing or the password is blank, `task up` stops and tells you what to fix instead of starting a broken database.

Task uses Docker by default. On Podman, add `CONTAINER=podman` to your `.env`. (The PowerShell `docker` alias doesn't work inside Task, so use this instead.)

## Connect DBeaver

New connection, pick **PostgreSQL**. Then: host `localhost`, port `5432` (or your `POSTGRES_PORT`), database `postgres`, user `ccos-db`, and your `POSTGRES_PASSWORD` (`ccos-dev-password` unless you changed it).
Hit **Test Connection**. If it asks to download a driver, say yes. Green = done.

The tables only show up after you've run `task migrate` once. Look under `postgres` > `Schemas` > `public` > `Tables`.

## If it breaks

- **`28P01 password authentication failed for user "ccos-db"`**: the password in your user secret or DBeaver doesn't match the one the database was created with. PostgreSQL only reads `POSTGRES_PASSWORD` the first time the volume is made, so editing `.env` later changes nothing. Keep your data: `docker compose exec ccos-db psql -U ccos-db -d postgres -c "ALTER ROLE CURRENT_USER PASSWORD 'ccos-dev-password'"` (swap `docker` for `podman` if that's you). Don't care about the data: `task reset` then `task migrate`.
- **`ccos-db` starts then stops**: `docker compose logs ccos-db` shows why. An empty `POSTGRES_PASSWORD` in `.env` is the usual cause.
- **"port is already allocated"**: something else (usually a PostgreSQL you installed on Windows) is on 5432. Either stop that one, or set `POSTGRES_PORT=5433` in `.env` and use port 5433 in DBeaver and your user secret.
- **Stop it**: `docker compose stop`. **Start again**: `docker compose up -d ccos-db`. Your data stays.

## Why PostgreSQL (decided 2026-10-14, CAR-45)

The team settled on PostgreSQL 17 for CAR-45. It runs the same everywhere (Windows, Mac, Linux, CI), it's free, and the Npgsql provider covers everything we use in EF Core.

- Event overselling (CAR-30) stays blocked. Concurrency uses PostgreSQL's built-in `xmin` column as the EF Core concurrency token, and booking still does one conditional `UPDATE`.
- Times are stored as `timestamp(0) with time zone` with `now()` defaults.
- The migrations were regenerated from scratch as one `InitialPostgresSchema` migration.

SQLite is still allowed for quick local testing: run the API in Development with no
connection string and it uses a sample SQLite file (see `backend/README.md`).
