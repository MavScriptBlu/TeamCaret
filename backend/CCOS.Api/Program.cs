using CCOS.Api;
using CCOS.Data;
using CCOS.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Tests set this flag so Program.cs doesn't register its own DbContext (tests bring their own).
var isTestMode = builder.Configuration.GetValue<bool>("Testing");

// ---- Services (things the app can use) ----
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Finds the controller classes (like ProductsController) and turns them into endpoints.
builder.Services.AddControllers();

// ─────────────────────────────────────────────────────────────────────────────
// Database Configuration
// ─────────────────────────────────────────────────────────────────────────────
// Production/Staging: PostgreSQL via ConnectionStrings:CCOS (required).
// Development: PostgreSQL when ConnectionStrings:CCOS is set (recommended),
//              otherwise falls back to a local SQLite file (ccos-dev.db) so
//              the API can run without Docker or a database server.
//
// To use PostgreSQL in Development:
//   1. Start the database: docker compose up -d ccos-db (or: task db)
//   2. Set the connection string in user secrets (see backend/README.md step 3)
//   3. Run migrations: task migrate
//
// The SQLite fallback is intentional for quick local runs without Docker,
// but PostgreSQL is the only supported database for tests and production.
// See backend/README.md "No database? No problem" for details.
// ─────────────────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("CCOS");
var useLocalDatabase = string.IsNullOrWhiteSpace(connectionString);

if (!isTestMode && useLocalDatabase && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "ConnectionStrings:CCOS must be set outside Development. " +
        "See backend/README.md for setup instructions.");
}

if (!isTestMode)
{
    // Hands out a database connection (AppDbContext) to anything that asks for one.
    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        // SQLite fallback for Development only when no connection string is configured.
        // This file is gitignored and auto-created with seed data on first run.
        if (useLocalDatabase)
        {
            var databasePath = Path.Combine(builder.Environment.ContentRootPath, "ccos-dev.db");
            options.UseSqlite($"Data Source={databasePath}");
        }
        else
        {
            // PostgreSQL: the production database.
            // Use this in Development too for consistency with prod.
            options.UseNpgsql(connectionString);
        }
    });
}

// One repository per web request, so it shares that request's database connection.
builder.Services.AddScoped<ProductRepository>();
builder.Services.AddScoped<EventRepository>();
builder.Services.AddScoped<VenueRepository>();

var app = builder.Build();

// ---- Startup work ----
// Development: create the tables if needed and add sample products (SQLite or PostgreSQL).
// Skip in test mode - tests manage their own database state.
if (app.Environment.IsDevelopment() && !isTestMode)
{
    await DevelopmentData.EnsureCreatedAndSeededAsync(app.Services, useLocalDatabase);
}

// ---- Request pipeline (what happens to each incoming request, in order) ----
if (app.Environment.IsDevelopment())
{
    // Publishes the API description (OpenAPI) so tools can read what endpoints exist.
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();
app.Run();
