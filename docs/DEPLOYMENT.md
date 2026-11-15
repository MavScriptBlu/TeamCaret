# Deploying CCOS

How to run CCOS somewhere other than your own computer (CAR-84). It doesn't depend on a
particular host. We're waiting on Troy to pick the host and database; once he does, add a short
section for that host at the bottom.

CCOS is two apps and a database:

| Piece | Project | Needs |
|---|---|---|
| API | `backend/CCOS.Api` | A PostgreSQL database |
| Website | `frontend/CCOS.Web` | The API's address |
| Database | PostgreSQL 17 | Nothing else |

Both apps are .NET 10 and each has a Dockerfile (`backend/CCOS.Api/Dockerfile`,
`frontend/CCOS.Web/Dockerfile`), so any host that runs .NET 10 or containers works. Build both
images from the repo root, since the API and website both need `shared/`.

## 1. Settings

Nothing host-specific is in the code. Everything comes from settings, which a host sets as
environment variables. Environment variables use a double underscore where the setting name has
a colon (`ConnectionStrings:CCOS` becomes `ConnectionStrings__CCOS`).

### API

| Environment variable | Required | What it is |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Yes | `Production` on a real host. `Development` adds sample data and logins, so don't use it there. |
| `ConnectionStrings__CCOS` | Yes | The PostgreSQL connection string, for example `Host=...;Port=5432;Database=...;Username=...;Password=...`. The API won't start outside Development without it. |
| `Database__MigrateOnStartup` | No | `true` makes the API create and update the tables each time it starts (see step 2). Default `false`. |

### Website

| Environment variable | Required | What it is |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Yes | `Production` on a real host. |
| `Api__BaseUrl` | Yes | The API's address, ending in `/`, for example `https://ccos-api.example.com/`. The site won't start without it. |
| `DataProtection__KeysPath` | Recommended | A folder that survives restarts. The site keeps the keys for its sign-in cookies there. Without it, every restart signs everyone out. |

Keep the connection string and passwords in the host's secret settings, never in the repo.

## 2. Database tables

Outside Development nothing creates the tables by itself. Pick one:

- **From your computer** (needs network access to the database):

  ```
  dotnet tool restore
  $env:CCOS_CONNECTION_STRING = "Host=...;Port=5432;Database=...;Username=...;Password=..."
  dotnet ef database update --project backend/CCOS.Data
  ```

  (On macOS or Linux: `export CCOS_CONNECTION_STRING="..."`.) This is the same as `task migrate`,
  pointed at the hosted database.

- **On startup:** set `Database__MigrateOnStartup=true` on the API. It applies any new migrations
  each time it starts, which is easiest when the database can't be reached from outside the host.

Both are safe to run again; migrations that are already applied are skipped.

Outside Development there's no sample data and no sample logins. Accounts are created through
the website's Create account page.

## 3. Start the apps

Start the API first, then the website with `Api__BaseUrl` pointing at the API. With the
Dockerfiles, both listen on port 8080 inside the container.

## 4. Check it works

Both apps have a health check that needs no sign-in:

| URL | Healthy (200) means |
|---|---|
| `<api address>/health` | The API is up and can reach the database. |
| `<website address>/health` | The website is up and the API's `/health` is healthy. |

Each answers `Healthy`, or `Unhealthy` with status 503. Point the host's health check or uptime
monitor at the website's `/health`, since it checks all three pieces at once.

Then open the website, create an account, and log in.

## Still to decide

- The host and database (waiting on Troy, CAR-87).
- How the club's first officer gets set up on a fresh database.
- Whether the demo uses the hosted site with real data, or the Docker setup with sample data.
