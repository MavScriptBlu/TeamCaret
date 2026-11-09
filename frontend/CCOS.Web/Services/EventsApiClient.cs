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

    /// <summary>
    /// Every event, removed and past ones too, from GET /api/events/officer (CAR-56).
    /// For the officer event list.
    /// </summary>
    public async Task<IReadOnlyList<EventDto>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return await http.GetFromJsonAsync<List<EventDto>>("api/events/officer", cancellationToken) ?? [];
    }

    /// <summary>
    /// One event in any state from GET /api/events/officer/{id}, or null when there's no event with
    /// that id. A removed event is returned too (IsActive false) so the edit page can restore it.
    /// </summary>
    public async Task<EventDto?> GetForOfficerAsync(int eventId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/events/officer/{eventId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EventDto>(cancellationToken);
    }

    /// <summary>
    /// Sends a new event to POST /api/events (CAR-55). The caller checks the returned response
    /// (success or validation errors) and disposes it.
    /// </summary>
    public Task<HttpResponseMessage> CreateAsync(CreateEventRequest request, CancellationToken cancellationToken = default)
    {
        return http.PostAsJsonAsync("api/events", request, cancellationToken);
    }

    /// <summary>
    /// Sends changes to PUT /api/events/{id}. IsActive true restores a removed event. The caller
    /// checks the returned response (success, not found, conflict or validation errors) and disposes it.
    /// </summary>
    public Task<HttpResponseMessage> UpdateAsync(
        int eventId,
        UpdateEventRequest request,
        CancellationToken cancellationToken = default)
    {
        return http.PutAsJsonAsync($"api/events/{eventId}", request, cancellationToken);
    }

    /// <summary>
    /// Removes an event with DELETE /api/events/{id}. It's a soft delete, so nothing is erased.
    /// The caller checks the returned response and disposes it.
    /// </summary>
    public Task<HttpResponseMessage> DeleteAsync(int eventId, CancellationToken cancellationToken = default)
    {
        return http.DeleteAsync($"api/events/{eventId}", cancellationToken);
    }
}
