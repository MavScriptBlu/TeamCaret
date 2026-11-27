using System.Net;
using System.Text.Json;
using CCOS.Shared;

namespace CCOS.Web.Services;

/// <summary>
/// Calls the order endpoints in CCOS.Api (CAR-91). The signed-in person's API token is added by
/// <see cref="ApiBearerTokenHandler"/>, so the order belongs to them.
/// </summary>
public sealed class OrdersApiClient(HttpClient http)
{
    /// <summary>
    /// Places one order with POST /api/orders. Prices and stock are checked by the API, never sent
    /// from here. Returns the saved order (201), or the status code with the API's sentence and the
    /// productId of the item that stopped the order (403 members only, 409 sold out or out of stock).
    /// Network problems and timeouts are thrown for the page to catch.
    /// </summary>
    /// <param name="request">The cart's product or ticket type ids and quantities.</param>
    /// <param name="cancellationToken">Stops the call when the visitor leaves.</param>
    public async Task<PlaceOrderResponse> PlaceAsync(PlaceOrderRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/orders", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Created)
        {
            var order = await response.Content.ReadFromJsonAsync<OrderDto>(cancellationToken)
                ?? throw new JsonException("The order API answered 201 with no order.");
            return new PlaceOrderResponse(order, response.StatusCode, null, null);
        }

        // 403 and 409 answers are problem details: a "detail" sentence that names the item, and
        // the item's "productId" so the page can point at the right cart line.
        string? message = null;
        int? productId = null;
        try
        {
            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var problem = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
            if (problem.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (problem.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                {
                    message = detail.GetString();
                }

                if (problem.RootElement.TryGetProperty("productId", out var id) && id.ValueKind == JsonValueKind.Number)
                {
                    productId = id.GetInt32();
                }
            }
        }
        catch (JsonException)
        {
            // Not a problem-details body (an empty 401, say): the status code is enough.
        }

        return new PlaceOrderResponse(null, response.StatusCode, message, productId);
    }
}

/// <summary>
/// What POST /api/orders answered (CAR-91).
/// </summary>
/// <param name="Order">The saved order, when it was placed.</param>
/// <param name="StatusCode">The API's status code: 201 placed, 401 not signed in, 403 members only, 409 not available.</param>
/// <param name="Message">The API's sentence naming the item that stopped the order, or null.</param>
/// <param name="ProblemProductId">The product or ticket type id that stopped the order, or null.</param>
public sealed record PlaceOrderResponse(OrderDto? Order, HttpStatusCode StatusCode, string? Message, int? ProblemProductId)
{
    /// <summary>True when the order was saved.</summary>
    public bool IsPlaced => Order is not null;
}
