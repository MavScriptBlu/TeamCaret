# CCOS database setup (Postgres + Docker + DBeaver)

## Get Postgres running

1. Open Docker Desktop and make sure it says it's running.
2. Make a folder like `C:\Users\Blue\Dev\ccos-db` and drop `docker-compose.yml` in it.
3. Open the file, swap `PUT_YOUR_PASSWORD_HERE` for your real password (the one in DBeaver). Save.
4. Open a terminal in that folder and run:
   ```
   docker compose up -d
   ```
5. Check it's alive:
   ```
   docker ps
   ```
   You should see `ccos-db` with `0.0.0.0:5432->5432`.

## Connect DBeaver

Your settings are already right: host `localhost`, port `5432`, database `postgres`, user `ccos-db`, your password.
Hit **Test Connection**. If it asks to download a driver, say yes. Green = done.

## If it breaks

- **"password authentication failed"**: the password in the compose file and DBeaver don't match. If you changed it AFTER the first run, the old one is baked in. Fix: `docker compose down -v` (wipes the db, it's empty anyway) then `docker compose up -d`.
- **"port is already allocated"**: something else (usually a Postgres you installed on Windows) is on 5432. Either stop that one, or change the compose line to `"5433:5432"` and use port 5433 in DBeaver.
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
| Setup           | Needs Docker running                                      | Zero setup, it's just a file               |
| Feels like      | A real server, closer to what the club would actually run | Great for quick local testing              |

**Fair points for SQLite:** nobody needs Docker, nothing to install, the db is one file you can delete and recreate.

**Fair points for Postgres:** it's a real multi-user server (closer to production), and DBeaver + Docker is a solid skill to show off.

**The "both" option:** pick the provider from a setting (like `"DbProvider": "Postgres"` or `"Sqlite"` in appsettings) and have Program.cs choose. Catch: EF migrations get made for one provider at a time, so either keep two migration folders, or agree on one "official" db for migrations and use the other just for local messing around.

Simplest deal to pitch: **Postgres is the official one, SQLite is allowed for quick local testing.**
