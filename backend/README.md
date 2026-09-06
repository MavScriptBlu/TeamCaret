# backend/

The ASP.NET Core Web API + Entity Framework Core side of CCOS - the "Web API",
"repository", and "data" layers from the proposal's flow:

```
view -> API client -> Web API -> repository -> data
```

Planned projects (create with `dotnet new` when implementation starts):

- **CCOS.Api** - ASP.NET Core Web API. Controllers, `Program.cs`, auth (ASP.NET
  Core Identity + JWT), appsettings.
- **CCOS.Repositories** - Repository classes/interfaces wrapping EF Core, including
  the transactional order placement/cancellation logic (rollback on failure).
- **CCOS.Data** - `AppDbContext`, entity classes (Customer, Member, Officer, Product,
  Venue, Event, Order, OrderLine, Ticket), and EF Core migrations.

See `docs/CyberCougar OES Schema.md` and the ERD doc for the data model these
projects implement.
