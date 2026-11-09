using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CCOS.Data;
using CCOS.Data.Entities;
using CCOS.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CCOS.Api.Tests;

/// <summary>
/// Signs a test client in as a current officer, for tests that call officer-only endpoints (CAR-69).
/// The account is registered through the API, given a membership and a title straight in the
/// database (the way an admin screen will later), then logged in with a bearer token.
/// </summary>
internal static class TestAccounts
{
    // A fresh random password each run, so no credential is ever written in the code.
    private static readonly string TestSecret = $"T{Guid.NewGuid():N}!a1";

    public static async Task SignInAsOfficerAsync(WebApplicationFactory<Program> factory, HttpClient client)
    {
        var email = $"officer-{Guid.NewGuid():N}@ccos.test";
        var register = await client.PostAsJsonAsync(
            "/api/account/register",
            new RegisterAccountRequest("Test", "Officer", email, TestSecret));
        register.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.AsNoTracking().SingleAsync(account => account.Email == email);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            db.Members.Add(new Member { MemberId = user.CustomerId, JoinedDate = today.AddMonths(-6), MemberExpiration = today.AddMonths(6) });
            db.Officers.Add(new Officer { OfficerId = user.CustomerId, Title = "President", GrantedDate = today.AddMonths(-6) });
            await db.SaveChangesAsync();
        }

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestSecret });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
    }
}
