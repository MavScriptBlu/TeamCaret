using System.Net;
using System.Net.Http.Json;
using CCOS.Data;
using CCOS.Data.Entities;
using CCOS.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CCOS.Api.Tests;

/// <summary>
/// Exercises event concurrency against PostgreSQL because SQLite does not generate xmin values.
/// </summary>
public sealed class EventsConcurrencyPostgresTests(
    WebApplicationFactory<Program> factory,
    PostgresApiTestFixture postgres) : IClassFixture<WebApplicationFactory<Program>>,
    IClassFixture<PostgresApiTestFixture>, IAsyncLifetime
{
    private WebApplicationFactory<Program> _testFactory = null!;
    private HttpClient _client = null!;
    private int _eventId;

    public async Task InitializeAsync()
    {
        _testFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Testing", "true");
            builder.ConfigureTestServices(services =>
                services.AddDbContext<AppDbContext>(options => options.UseNpgsql(postgres.ConnectionString)));
        });

        using (var scope = _testFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();

            var venue = new Venue { Name = $"Concurrency Venue {Guid.NewGuid():N}", Capacity = 100 };
            var startsAt = DateTime.UtcNow.AddDays(10);
            var @event = new Event
            {
                Venue = venue,
                Name = "Original event name",
                Description = "Original event description",
                StartsAtUtc = startsAt,
                EndsAtUtc = startsAt.AddHours(2),
                TicketCapacity = 50,
                IsActive = true,
            };
            @event.TicketTypes.Add(new TicketType
            {
                Category = "Tickets",
                Name = "General",
                Price = 10m,
                MemberPrice = 5m,
                StockQuantity = 0,
                IsActive = true,
            });

            db.Events.Add(@event);
            await db.SaveChangesAsync();
            _eventId = @event.EventId;
        }

        _client = _testFactory.CreateClient();
        await TestAccounts.SignInAsOfficerAsync(_testFactory, _client);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _testFactory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Put_with_stale_copy_returns_conflict_without_overwriting_first_save()
    {
        var firstCopy = await GetEventAsync();
        var secondCopy = await GetEventAsync();
        Assert.Equal(firstCopy.RowVersion, secondCopy.RowVersion);

        using var firstResponse = await _client.PutAsJsonAsync(
            $"/api/events/{_eventId}",
            CreateUpdate(firstCopy, name: "First tab name", description: firstCopy.Description));

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstSave = await firstResponse.Content.ReadFromJsonAsync<EventDto>();
        Assert.NotNull(firstSave);
        Assert.NotEqual(firstCopy.RowVersion, firstSave.RowVersion);

        // Tab 2 edits only the description, but submits the old name and the original token.
        using var staleResponse = await _client.PutAsJsonAsync(
            $"/api/events/{_eventId}",
            CreateUpdate(secondCopy, name: secondCopy.Name, description: "Second tab description"));

        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
        var saved = await GetEventAsync();
        Assert.Equal("First tab name", saved.Name);
        Assert.Equal("Original event description", saved.Description);
    }

    [Fact]
    public async Task Put_with_current_token_saves_and_returns_a_new_token()
    {
        var current = await GetEventAsync();

        using var response = await _client.PutAsJsonAsync(
            $"/api/events/{_eventId}",
            CreateUpdate(current, name: "Updated event name", description: current.Description));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<EventDto>();
        Assert.NotNull(updated);
        Assert.Equal("Updated event name", updated.Name);
        Assert.NotEqual(current.RowVersion, updated.RowVersion);
    }

    private async Task<EventDto> GetEventAsync()
    {
        var result = await _client.GetFromJsonAsync<EventDto>($"/api/events/officer/{_eventId}");
        return result ?? throw new InvalidOperationException("The seeded event could not be loaded.");
    }

    private static UpdateEventRequest CreateUpdate(EventDto source, string name, string? description)
    {
        var ticketType = source.TicketTypes.Single();
        return new UpdateEventRequest(
            name,
            description,
            source.StartsAtUtc,
            source.EndsAtUtc,
            source.VenueId,
            source.TicketCapacity,
            source.MembersOnly,
            ticketType.Price,
            ticketType.MemberPrice ?? throw new InvalidOperationException("The officer response omitted the member price."),
            source.IsActive,
            source.RowVersion);
    }
}