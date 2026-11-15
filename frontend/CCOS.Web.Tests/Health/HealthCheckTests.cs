using System.Net;
using CCOS.Web.Health;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;

namespace CCOS.Web.Tests.Health;

/// <summary>
/// The website's GET /health (CAR-84): healthy only when the API's own /health says so.
/// </summary>
public class HealthCheckTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    // The real site, with the API's /health answered by a fake.
    private HttpClient CreateClient(MockHttpMessageHandler fakeApi) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Api:BaseUrl", "https://api.test/");
            builder.ConfigureTestServices(services =>
                services.AddHttpClient(ApiHealthCheck.ClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => fakeApi));
        }).CreateClient();

    [Fact]
    public async Task Health_ApiHealthy_IsHealthy()
    {
        using var fakeApi = new MockHttpMessageHandler();
        fakeApi.When("https://api.test/health").Respond("text/plain", "Healthy");
        using var client = CreateClient(fakeApi);

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_ApiUnhealthy_Is503()
    {
        // The API is up but can't reach its database.
        using var fakeApi = new MockHttpMessageHandler();
        fakeApi.When("https://api.test/health").Respond(HttpStatusCode.ServiceUnavailable, "text/plain", "Unhealthy");
        using var client = CreateClient(fakeApi);

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_ApiNotRunning_Is503()
    {
        // Nothing answers on port 9, like when CCOS.Api isn't running.
        using var client = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Api:BaseUrl", "https://127.0.0.1:9/")).CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
