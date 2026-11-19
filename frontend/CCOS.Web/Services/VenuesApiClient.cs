using System.Net;
using CCOS.Shared;

namespace CCOS.Web.Services;

/// <summary>
/// Calls the venue endpoints in CCOS.Api (view → API client → Web API → repository → data).
/// The API address comes from Api:BaseUrl in appsettings.json.
/// </summary>
public sealed class VenuesApiClient(HttpClient http)
{
    /// <summary>
    /// Active venues from GET /api/venues, for the event form's venue dropdown.
    /// </summary>
    public async Task<IReadOnlyList<VenueDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await http.GetFromJsonAsync<List<VenueDto>>("api/venues", cancellationToken) ?? [];
    }

    /// <summary>
    /// Sends a new venue to POST /api/venues. The caller checks the returned response
    /// (success or validation errors) and disposes it.
    /// </summary>
    public Task<HttpResponseMessage> CreateAsync(CreateVenueRequest request, CancellationToken cancellationToken = default)
    {
        return http.PostAsJsonAsync("api/venues", request, cancellationToken);
    }

    /// <summary>
    /// Every venue, removed ones too, from GET /api/venues/officer (CAR-57). For the officer venue list.
    /// </summary>
    public async Task<IReadOnlyList<VenueDetailDto>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return await http.GetFromJsonAsync<List<VenueDetailDto>>("api/venues/officer", cancellationToken) ?? [];
    }

    /// <summary>
    /// One venue with its address from GET /api/venues/{id}, or null when there's no venue with that id.
    /// A removed venue is returned too (IsActive false).
    /// </summary>
    public async Task<VenueDetailDto?> GetAsync(int venueId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/venues/{venueId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<VenueDetailDto>(cancellationToken);
    }

    /// <summary>
    /// Sends changes to PUT /api/venues/{id}. IsActive true restores a removed venue. The caller
    /// checks the returned response (success, not found, or validation errors) and disposes it.
    /// </summary>
    public Task<HttpResponseMessage> UpdateAsync(
        int venueId,
        UpdateVenueRequest request,
        CancellationToken cancellationToken = default)
    {
        return http.PutAsJsonAsync($"api/venues/{venueId}", request, cancellationToken);
    }

    /// <summary>
    /// Removes a venue with DELETE /api/venues/{id}. It's a soft delete, so nothing is erased.
    /// The caller checks the returned response and disposes it.
    /// </summary>
    public Task<HttpResponseMessage> DeleteAsync(int venueId, CancellationToken cancellationToken = default)
    {
        return http.DeleteAsync($"api/venues/{venueId}", cancellationToken);
    }
}
