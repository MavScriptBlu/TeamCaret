using CCOS.Shared;

namespace CCOS.Web.Services;

/// <summary>
/// Reads the signed-in person's orders from GET /api/orders/mine (CAR-95), for the My orders page.
/// The API token is added by <see cref="ApiBearerTokenHandler"/>, so the API only ever returns
/// this person's own orders.
/// </summary>
public sealed class MyOrdersApiClient(HttpClient http)
{
    /// <summary>
    /// The signed-in person's orders, newest first. Network problems, timeouts and error answers
    /// (including 401 when the sign-in ran out) are thrown for the page to catch.
    /// </summary>
    /// <param name="cancellationToken">Stops the call when the visitor leaves.</param>
    public async Task<IReadOnlyList<OrderDto>> GetMineAsync(CancellationToken cancellationToken = default)
    {
        return await http.GetFromJsonAsync<List<OrderDto>>("api/orders/mine", cancellationToken) ?? [];
    }
}
