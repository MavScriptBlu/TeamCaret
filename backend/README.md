# backend/

The ASP.NET Core Web API + Entity Framework Core side of CCOS - the "Web API",
"repository", and "data" layers from the proposal's flow:

```
view -> API client -> Web API -> repository -> data
```

Projects:

- **CCOS.Api** - Planned ASP.NET Core Web API. Controllers, `Program.cs`, auth
  (ASP.NET Core Identity + JWT), appsettings.
- **CCOS.Repositories** - EF Core repositories, including transactional event-ticket
  reservation and cancellation.
- **CCOS.Data** - `AppDbContext`, data entities, and EF Core migrations. The event,
  venue, ticket-type, and ticket-registration schema is implemented here.
- **CCOS.Data.Tests** - SQLite-backed tests for model configuration and ticket booking.

See `docs/CyberCougar OES Schema.md` and the ERD doc for the data model these
projects implement.

Run the backend tests from the repository root:

```sh
dotnet test backend/CCOS.slnx
```

The event schema is an incremental migration after `InitialProductOrderSchema`.
Applying migrations requires a SQL Server connection supplied as
`CCOS_CONNECTION_STRING`; this repository's sandbox generated the migration but
did not apply it to a database. To apply it locally or in CI, install the matching
EF Core CLI and run:

```sh
dotnet tool install --global dotnet-ef --version 10.0.12
dotnet ef database update \
  --project backend/CCOS.Data/CCOS.Data.csproj \
  --startup-project backend/CCOS.Data/CCOS.Data.csproj
```
