# backend/

The ASP.NET Core Web API + Entity Framework Core side of CCOS. These are the "Web API",
"repository", and "data" layers from the proposal's flow:

```structure
view -> API client -> Web API -> repository -> data
```

- **CCOS.Api**: ASP.NET Core Web API. Controllers, `Program.cs`, appsettings.
- **CCOS.Repositories**: repository classes that wrap EF Core, including the ticket
  booking logic (rolls back if an event sells out).
- **CCOS.Data**: `AppDbContext`, the entity classes, and the EF Core migrations.
- **CCOS.Data.Tests**: xUnit tests for the data layer.

See `docs/CyberCougar OES Schema.md` and the ERD doc for the data model.

## Set up the database (PostgreSQL in Docker)

Do this once per computer. After that it's just step 2 and step 5.

### 1. Install Docker Desktop

Get it from <https://www.docker.com/products/docker-desktop> and open it. Wait until it
says it's running. (On Podman? `podman machine start` instead, details in
`docs/database-setup.md`.)

You also need the .NET 10 SDK, which you already have if you can build the solution.

### 2. Start the database container

From the repo root:

1. Copy `.env.example`, rename the copy to `.env`. The shared dev password inside
   (`ccos-dev-password`) is fine to keep. It only protects a database on your own computer.
2. Start it:

   ```powershell
   docker compose up -d ccos-db
   ```

   (or `task db` if you have Task). `docker ps` should show `ccos-db` with
   `0.0.0.0:5432->5432`. Give it a few seconds to wake up the first time.

The data lives in a Docker volume (`ccos-data`), so it's still there after you
stop, restart, or reboot. Only `task reset` or `docker compose down -v` wipes it.

### 3. Tell the API where the database is (user secrets)

The connection string has your password in it, so it goes in **user secrets**, not in
`appsettings.json`. User secrets live in your Windows user folder, outside the repo, so
they can't get committed by accident.

From the repo root (swap the password if you changed it in `.env`):

```powershell
dotnet user-secrets set "ConnectionStrings:CCOS" "Host=localhost;Port=5432;Database=postgres;Username=ccos-db;Password=ccos123" --project backend/CCOS.Api
```

Check it saved with `dotnet user-secrets list --project backend/CCOS.Api`.

### 4. Create the tables (apply the migrations)

```powershell
dotnet tool restore
dotnet ef database update --project backend/CCOS.Data
```

or just `task migrate`. `dotnet tool restore` installs the `dotnet ef` version the repo
pins in `.config/dotnet-tools.json`, so everyone runs the same one.

`dotnet ef` reads the same user secret the API uses, so there's nothing else to set.
It's safe to run again any time, it skips migrations that are already applied.

### 5. Run the API

```powershell
dotnet run --project backend/CCOS.Api
```

Open the `/api/products` URL it prints. Anything you save now is in PostgreSQL, so it's
still there next time and everyone on the same database sees the same data.

## The API container

`task up` also runs the API in a container (`ccos-api`). It gets its connection string from
`docker-compose.yml` (host `ccos-db`, built from your `.env`), so it uses the same PostgreSQL
container as `dotnet run`. It does not use your user secret. Run `task migrate` once so the tables exist.

## Sample accounts

In Development, the API adds three sample logins the first time it finds no customers
(CAR-66). They share one password, which is kept out of the code: agree on one with the team
(ask in the team chat) and set it once on your own computer. It's for local testing only, so
never reuse a real password.

```powershell
# For dotnet run (the API on your computer)
dotnet user-secrets set "SampleAccounts:Password" "the-team-password" --project backend/CCOS.Api

# For task up (the API in Docker): add this line to your .env instead
# SAMPLE_ACCOUNT_PASSWORD=the-team-password
```

If it isn't set, the API skips the sample logins and logs a warning. Ran the API before
setting it? The logins are only added when there are no customers yet, so start fresh with
`task reset` then `task migrate` (or delete `ccos-dev.db` if you use the SQLite file).

| Email | Who they are |
| --- | --- |
| `officer@ccos.test` | Current member who holds the President title (officer pages) |
| `member@ccos.test` | Current member (member prices, members-only events) |
| `guest@ccos.test` | Has a login, but isn't a member |

Memberships run one year from the day they were added. Signing in arrives with CAR-67 and
CAR-68; until then the accounts are only in the database.

## No database? No problem

If there's no `ConnectionStrings:CCOS` (no user secret set), the API in **Development**
falls back to a local SQLite file (`backend/CCOS.Api/ccos-dev.db`) with sample products.
Handy for quick UI work. Anywhere other than Development, a missing connection string
stops the API with an error on purpose.

The SQLite file is only built once. If yours was made before a table was added (for example
the CAR-66 login tables), delete `backend/CCOS.Api/ccos-dev.db` and run the API again to rebuild it.

To switch from PostgreSQL back to the SQLite sample:

```powershell
dotnet user-secrets remove "ConnectionStrings:CCOS" --project backend/CCOS.Api
```

## Adding a migration

After you change an entity or `AppDbContext`:

```powershell
dotnet ef migrations add YourChangeName --project backend/CCOS.Data
dotnet ef database update --project backend/CCOS.Data
```

## If it breaks

- **"No database connection string found"** from `dotnet ef`: step 3 isn't done yet.
- **`28P01 password authentication failed for user "ccos-db"`**: the password in your user
  secret doesn't match the one PostgreSQL was first started with. PostgreSQL only reads
  `POSTGRES_PASSWORD` when the volume is created, so changing `.env` later doesn't change it.
  Fastest fix without losing data (works with Podman too, swap `docker` for `podman`):
  `docker compose exec ccos-db psql -U ccos-db -d postgres -c "ALTER ROLE CURRENT_USER PASSWORD 'ccos-dev-password'"`.
  Or `task reset` wipes the volume and starts fresh (then redo step 4).
- **`ccos-db` keeps stopping**: run `docker compose logs ccos-db` to see the complaint.
  An empty `POSTGRES_PASSWORD` in `.env` is the usual cause.
- **"port is already allocated"**: a PostgreSQL installed on Windows is already on 5432.
  Set `POSTGRES_PORT=5433` in `.env` and use `Port=5433` in your user secret.
