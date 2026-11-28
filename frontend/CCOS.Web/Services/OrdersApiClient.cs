using CCOS.Shared;

namespace CCOS.Web.Services;

/// <summary>Calls the officer order endpoints in CCOS.Api.</summary>
public sealed class OrdersApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<OfficerOrderDto>> ListAsync(
        string? status,
        CancellationToken cancellationToken = default)
    {
        string path = string.IsNullOrEmpty(status)
            ? "api/orders"
            : $"api/orders?status={Uri.EscapeDataString(status)}";

        return await http.GetFromJsonAsync<List<OfficerOrderDto>>(path, cancellationToken) ?? [];
    }

    public Task<HttpResponseMessage> ChangeStatusAsync(
        int orderId,
        ChangeOrderStatusRequest request,
        CancellationToken cancellationToken = default) =>
        http.PutAsJsonAsync($"api/orders/{orderId}/status", request, cancellationToken);
}
