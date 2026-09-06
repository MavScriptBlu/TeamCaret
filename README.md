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

## Tech Stack

- ASP.NET Core MVC (Web API backend + Razor Pages/Blazor frontend - team's
  final call, see proposal)
- Entity Framework Core
- SQL Server (Azure SQL in production)
- ASP.NET Core Identity for authentication
- Deployed to Azure App Service

## Project Structure

```text
TeamCaret/
├── backend/        ASP.NET Core Web API + EF Core (Web API, repository, data layers)
├── frontend/        Razor Pages/Blazor app that calls the backend API (view layer)
├── shared/         DTOs/enums shared between backend and frontend (the API contract)
├── docs/           Proposal, ERD, schema, backlog, changelog
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
