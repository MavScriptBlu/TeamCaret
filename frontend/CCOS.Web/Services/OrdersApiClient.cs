using CCOS.Shared;

namespace CCOS.Web.Services;

/// <summary>Calls the officer order endpoints in CCOS.Api.</summary>
public sealed class OrdersApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<OrderDto>> ListAsync(string? status, CancellationToken cancellationToken = default)
    {
        var path = string.IsNullOrEmpty(status)
            ? "api/orders"
            : $"api/orders?status={Uri.EscapeDataString(status)}";

        return await http.GetFromJsonAsync<List<OrderDto>>(path, cancellationToken) ?? [];
    }

    public Task<HttpResponseMessage> ChangeStatusAsync(
        int orderId,
        ChangeOrderStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        return http.PutAsJsonAsync($"api/orders/{orderId}/status", request, cancellationToken);
    }
}