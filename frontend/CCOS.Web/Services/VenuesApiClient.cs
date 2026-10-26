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
}
