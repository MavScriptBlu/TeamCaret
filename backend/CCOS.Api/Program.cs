using CCOS.Api;
using CCOS.Data;
using CCOS.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();

// Database: PostgreSQL when ConnectionStrings:CCOS is set.
// Without it, Development uses a local SQLite file so the app runs without a database server.
var connectionString = builder.Configuration.GetConnectionString("CCOS");
var useLocalDatabase = string.IsNullOrWhiteSpace(connectionString);

if (useLocalDatabase && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("ConnectionStrings:CCOS must be set outside Development.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (useLocalDatabase)
    {
        var databasePath = Path.Combine(builder.Environment.ContentRootPath, "ccos-dev.db");
        options.UseSqlite($"Data Source={databasePath}");
    }
    else
    {
        options.UseNpgsql(connectionString);
    }
});
builder.Services.AddScoped<ProductRepository>();

var app = builder.Build();

// Development: create the tables if needed and add sample products (SQLite or PostgreSQL).
if (app.Environment.IsDevelopment())
{
    await DevelopmentData.EnsureCreatedAndSeededAsync(app.Services, useLocalDatabase);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
