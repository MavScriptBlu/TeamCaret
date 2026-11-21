using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;

namespace CCOS.Web.Tests;

/// <summary>
/// A website session kept in memory, so the cart (CAR-92) can be tested without a running site.
/// </summary>
internal sealed class TestSession : ISession
{
    private readonly Dictionary<string, byte[]> _values = new();

    public string Id => "test-session";

    public bool IsAvailable => true;

    public IEnumerable<string> Keys => _values.Keys;

    public void Clear() => _values.Clear();

    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Remove(string key) => _values.Remove(key);

    public void Set(string key, byte[] value) => _values[key] = value;

    public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value) => _values.TryGetValue(key, out value);
}

/// <summary>
/// Sets up a page model with a session (holding an optional cart), TempData and an optional
/// signed-in member, for the cart tests (CAR-92).
/// </summary>
internal static class CartTestContext
{
    public static HttpContext Attach(PageModel model, Cart? cart = null, bool member = false)
    {
        var http = new DefaultHttpContext();
        http.Features.Set<ISessionFeature>(new SessionFeature(new TestSession()));
        if (member)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AccountSession.MemberClaim, "true")], "Test"));
        }

        if (cart is not null)
        {
            http.SaveCart(cart);
        }

        model.PageContext = new PageContext { HttpContext = http };
        model.TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>());
        return http;
    }

    private sealed class SessionFeature(ISession session) : ISessionFeature
    {
        public ISession Session { get; set; } = session;
    }
}
