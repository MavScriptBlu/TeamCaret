
/// <summary>
/// Provides methods for interacting with the product endpoints in the CCOS API.
/// </summary>
using System.Net;
using CCOS.Shared;

namespace CCOS.Web.Services;

/// <summary>
/// Calls the product endpoints in CCOS.Api (view → API client → Web API → repository → data).
/// The API address comes from Api:BaseUrl in appsettings.json.
/// </summary>
public sealed class ProductsApiClient(HttpClient http)
{
    /// <summary>
    /// Active merchandise from GET /api/products.
    /// </summary>
    public async Task<IReadOnlyList<ProductDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await http.GetFromJsonAsync<List<ProductDto>>("api/products", cancellationToken) ?? [];
    }

    /// <summary>
    /// All merchandise, including inactive products, from GET /api/products?includeInactive=true.
    /// For the officer product list.
    /// </summary>
    public async Task<IReadOnlyList<ProductDto>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return await http.GetFromJsonAsync<List<ProductDto>>("api/products?includeInactive=true", cancellationToken) ?? [];
    }

    /// <summary>
    /// One active product from GET /api/products/{id}, or null when there's no active product with that id.
    /// </summary>
    public async Task<ProductDto?> GetAsync(int productId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/products/{productId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductDto>(cancellationToken);
    }

    /// <summary>
    /// Sends a new product to POST /api/products. The caller checks the returned response
    /// (success or validation errors) and disposes it.
    /// </summary>
    public Task<HttpResponseMessage> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        return http.PostAsJsonAsync("api/products", request, cancellationToken);
    }

    /// <summary>
    /// Finds one product (active or not) in the officer list, or null when the id isn't there.
    /// </summary>
    public async Task<ProductDto?> GetForEditAsync(int productId, CancellationToken cancellationToken = default)
    {
        // There is no "get one including inactive" endpoint, so load the full officer list
        // and pick the product out of it. That also lets officers edit deactivated products.
        var products = await ListAllAsync(cancellationToken);

        return products.FirstOrDefault(product => product.ProductId == productId);
    }

    /// <summary>
    /// Sends updated product data to PUT /api/products/{id}. The caller checks the returned
    /// response and disposes it.
    /// </summary>
    public Task<HttpResponseMessage> UpdateAsync(int productId, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        return http.PutAsJsonAsync($"api/products/{productId}", request, cancellationToken);
    }
}
