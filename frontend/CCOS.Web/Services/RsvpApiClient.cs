using System.Net;
using System.Text.Json;
using CCOS.Shared;

namespace CCOS.Web.Services;

/// <summary>
/// Calls the RSVP endpoints in CCOS.Api (CAR-94) for the signed-in person. Their API token is
/// added by <see cref="ApiBearerTokenHandler"/>.
/// </summary>
public sealed class RsvpApiClient(HttpClient http)
{
    /// <summary>Whether the signed-in person is going to the event. Errors are thrown for the page to catch.</summary>
    public async Task<RsvpStatusDto?> GetStatusAsync(int eventId, CancellationToken cancellationToken = default)
    {
        return await http.GetFromJsonAsync<RsvpStatusDto>($"api/events/{eventId}/rsvp", cancellationToken);
    }

    /// <summary>
    /// RSVPs to a free event. Returns the status code (200 going, 403 members only, 409 not free,
    /// sold out or closed) and the API's sentence when it didn't work.
    /// </summary>
    public async Task<RsvpResponse> RsvpAsync(int eventId, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsync($"api/events/{eventId}/rsvp", content: null, cancellationToken);
        return new RsvpResponse(response.StatusCode, await DetailAsync(response, cancellationToken));
    }

    /// <summary>Cancels the RSVP and gives the seat back. Returns 204 when canceled, 404 when there was none.</summary>
    public async Task<RsvpResponse> CancelAsync(int eventId, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync($"api/events/{eventId}/rsvp", cancellationToken);
        return new RsvpResponse(response.StatusCode, await DetailAsync(response, cancellationToken));
    }

    // The "detail" sentence of a problem answer, or null.
    private static async Task<string?> DetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return null;
        }

        try
        {
            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var problem = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
            return problem.RootElement.ValueKind == JsonValueKind.Object
                && problem.RootElement.TryGetProperty("detail", out var detail)
                && detail.ValueKind == JsonValueKind.String
                ? detail.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>What an RSVP call answered.</summary>
/// <param name="StatusCode">The API's status code.</param>
/// <param name="Message">The API's sentence when it didn't work, or null.</param>
public sealed record RsvpResponse(HttpStatusCode StatusCode, string? Message);
