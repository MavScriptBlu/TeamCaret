# CCOS database setup (Postgres + Docker or Podman + DBeaver)

## Get Postgres running

1. Open Docker Desktop and make sure it says it's running. (On Podman? Skip to [Using Podman](#using-podman) below.)
2. In the repo root, copy `.env.example` and rename the copy to `.env`. (`.env` is gitignored, never commit it.)
3. Open `.env` and set `POSTGRES_PASSWORD` to your own password. Save.
4. Open a terminal in the repo root and run:
   ```
   docker compose up -d
   ```
5. Check it's alive:
   ```
   docker ps
   ```
   You should see `ccos-db` with `0.0.0.0:5432->5432`.

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
3. Do the `.env` steps from above (copy `.env.example` to `.env`, set your password).
4. From the repo root:
   ```
   podman compose up -d
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

| Command      | What it does                                                |
| ------------ | ----------------------------------------------------------- |
| `task up`    | Starts the database and shows what's running               |
| `task down`  | Stops the database (your data stays)                       |
| `task logs`  | Shows the database logs (Ctrl+C to quit)                   |
| `task reset` | WIPES the database and starts fresh (asks you first)       |
| `task`       | Lists all of these (plus `task build` and `task test`)      |

Want the details on one? Run `task --summary up` (or `reset`). If your `.env` is missing or the password is blank, `task up` stops and tells you what to fix instead of starting a broken database.

Task uses Docker by default. On Podman, add `CONTAINER=podman` to your `.env`. (The PowerShell `docker` alias doesn't work inside Task, so use this instead.)

## Connect DBeaver

Use the values from your `.env`: host `localhost`, port `POSTGRES_PORT` (5432), database `POSTGRES_DB` (postgres), user `POSTGRES_USER` (ccos-db), and your password.
Hit **Test Connection**. If it asks to download a driver, say yes. Green = done.

## If it breaks

- **"password authentication failed"**: the password in `.env` and DBeaver don't match. If you changed it AFTER the first run, the old one is baked in. Fix: `docker compose down -v` (wipes the db, it's empty anyway) then `docker compose up -d`.
- **"port is already allocated"**: something else (usually a Postgres you installed on Windows) is on 5432. Either stop that one, or set `POSTGRES_PORT=5433` in `.env` and use port 5433 in DBeaver.
- **Stop it**: `docker compose stop`. **Start again**: `docker compose up -d`. Your data stays.

## Connection string for the app (hand this to Dylan)

```
Host=localhost;Port=5432;Database=postgres;Username=ccos-db;Password=YOUR_PASSWORD
```

Keep the real password out of GitHub (user secrets, or an appsettings.Development.json that's gitignored).

---

## Postgres vs SQLite: the team convo

Heads up: the original proposal says **SQL Server**. Whatever the team lands on, update the proposal so it matches.

**Good news: you don't really have to pick for the whole team.** CCOS uses EF Core, and EF Core can talk to either one. The difference in code is one NuGet package plus one line in `Program.cs`:

|                 | Postgres                                                  | SQLite                                     |
| --------------- | --------------------------------------------------------- | ------------------------------------------ |
| NuGet package   | `Npgsql.EntityFrameworkCore.PostgreSQL`                   | `Microsoft.EntityFrameworkCore.Sqlite`     |
| Program.cs line | `options.UseNpgsql(connString)`                           | `options.UseSqlite("Data Source=ccos.db")` |
| Setup           | Needs Docker or Podman running                            | Zero setup, it's just a file               |
| Feels like      | A real server, closer to what the club would actually run | Great for quick local testing              |

**Fair points for SQLite:** nobody needs Docker or Podman, nothing to install, the db is one file you can delete and recreate.

**Fair points for Postgres:** it's a real multi-user server (closer to production), and DBeaver + Docker is a solid skill to show off.

**The "both" option:** pick the provider from a setting (like `"DbProvider": "Postgres"` or `"Sqlite"` in appsettings) and have Program.cs choose. Catch: EF migrations get made for one provider at a time, so either keep two migration folders, or agree on one "official" db for migrations and use the other just for local messing around.

Simplest deal to pitch: **Postgres is the official one, SQLite is allowed for quick local testing.**
