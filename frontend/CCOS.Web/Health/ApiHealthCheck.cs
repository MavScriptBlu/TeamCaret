using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CCOS.Web.Health;

/// <summary>
/// The website's health check (CAR-84): healthy when the API answers its own /health, which
/// means the API is up and can reach the database. The website's GET /health answers
/// "Healthy" (200) or "Unhealthy" (503), so a host can tell whether the whole app works.
/// </summary>
public sealed class ApiHealthCheck(IHttpClientFactory httpClientFactory) : IHealthCheck
{
    /// <summary>The named HttpClient (set up in Program.cs) that points at the API.</summary>
    public const string ClientName = "ApiHealth";

    private static readonly Uri ApiHealthPath = new("health", UriKind.Relative);

    /// <summary>
    /// Calls the API's /health. Unhealthy when the API says so, can't be reached, or takes too long.
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(ClientName);

        try
        {
            using var response = await client.GetAsync(ApiHealthPath, cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("The API and its database can be reached.")
                : HealthCheckResult.Unhealthy($"The API answered {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return HealthCheckResult.Unhealthy("The API can't be reached.", ex);
        }
    }
}
