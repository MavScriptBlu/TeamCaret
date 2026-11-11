namespace CCOS.Web.Services;

/// <summary>
/// One link in the site menu (CAR-64).
/// </summary>
/// <param name="Text">The link text, for example "Manage venues".</param>
/// <param name="Href">Where the link goes, for example "/Officer/Venues".</param>
/// <param name="Section">The group heading it sits under: <see cref="SiteNav.ShopSection"/> or <see cref="SiteNav.OfficerSection"/>.</param>
/// <param name="OfficerOnly">True for links only officers see (CAR-69). The pages and the API check on their own too.</param>
/// <param name="MatchPrefix">
/// Also counts as the current page for pages under this path, for example the Edit pages under
/// "/Officer/Venues". Null when only the link's own page counts.
/// </param>
public sealed record NavLink(string Text, string Href, string Section, bool OfficerOnly = false, string? MatchPrefix = null);

/// <summary>
/// The site's menu links, kept in one place so the phone menu and the desktop sidebar
/// can't drift apart (CAR-64). Drawn by Pages/Shared/_NavLinks.cshtml.
/// </summary>
public static class SiteNav
{
    /// <summary>Heading for the links everyone sees.</summary>
    public const string ShopSection = "Shop";

    /// <summary>Heading for the officer-only links.</summary>
    public const string OfficerSection = "Officers";

    private const string IndexSuffix = "/Index";

    /// <summary>
    /// Every menu link, in the order they show.
    /// </summary>
    public static IReadOnlyList<NavLink> Links { get; } =
    [
        new("Merch", "/Products", ShopSection, MatchPrefix: "/Products"),
        new("Events", "/Events", ShopSection, MatchPrefix: "/Events"),
        new("Product dashboard", "/Officer/Products", OfficerSection, OfficerOnly: true, MatchPrefix: "/Officer/Products"),
        new("Create event", "/Officer/Events/Create", OfficerSection, OfficerOnly: true),
        new("Manage events", "/Officer/Events", OfficerSection, OfficerOnly: true, MatchPrefix: "/Officer/Events"),
        new("Add venue", "/Officer/Venues/Create", OfficerSection, OfficerOnly: true),
        new("Manage venues", "/Officer/Venues", OfficerSection, OfficerOnly: true, MatchPrefix: "/Officer/Venues"),
    ];

    /// <summary>
    /// The link for the page at <paramref name="path"/>, so the menu can highlight it, or null
    /// when the page isn't in the menu (for example Log in). A link's own page wins, then the
    /// closest <see cref="NavLink.MatchPrefix"/>: "/Officer/Events/Create" is Create event,
    /// while "/Officer/Events/Edit/2" is Manage events.
    /// </summary>
    /// <param name="path">The request path, for example "/Officer/Venues/Edit/3".</param>
    public static NavLink? CurrentFor(string? path)
    {
        var normalized = Normalize(path);

        var exact = Links.FirstOrDefault(link => string.Equals(link.Href, normalized, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        return Links
            .Where(link => link.MatchPrefix is not null && IsUnder(normalized, link.MatchPrefix))
            .OrderByDescending(link => link.MatchPrefix!.Length)
            .FirstOrDefault();
    }

    // "/Officer/Venues/Index/" and "/Officer/Venues" are the same page.
    private static string Normalize(string? path)
    {
        var trimmed = (path ?? "/").TrimEnd('/');
        if (trimmed.EndsWith(IndexSuffix, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^IndexSuffix.Length];
        }

        return trimmed.Length == 0 ? "/" : trimmed;
    }

    // Whole path segments only, so "/EventsArchive" isn't under "/Events".
    private static bool IsUnder(string path, string prefix) =>
        path.Equals(prefix, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
}
