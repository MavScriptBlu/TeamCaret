using System.Net;
using CCOS.Shared;

namespace CCOS.Web.Services;

/// <summary>
/// Calls the event endpoints in CCOS.Api (view → API client → Web API → repository → data).
/// The API address comes from Api:BaseUrl in appsettings.json.
/// </summary>
public sealed class EventsApiClient(HttpClient http)
{
    /// <summary>
    /// Upcoming events from GET /api/events: active, starting from now on, soonest first.
    /// </summary>
    public async Task<IReadOnlyList<EventDto>> ListUpcomingAsync(CancellationToken cancellationToken = default)
    {
        return await http.GetFromJsonAsync<List<EventDto>>("api/events", cancellationToken) ?? [];
    }

    /// <summary>
    /// One active event from GET /api/events/{id}, or null when there's no active event with that id.
    /// </summary>
    public async Task<EventDto?> GetAsync(int eventId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/events/{eventId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EventDto>(cancellationToken);
    }
}
