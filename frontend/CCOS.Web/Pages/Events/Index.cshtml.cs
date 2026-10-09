using System.Globalization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Events;

/// <summary>
/// Events home page (/Events): upcoming events and news.
/// Shows sample data for now. /Events?empty=true shows the empty state.
/// </summary>
public class IndexModel : PageModel
{
    /// <summary>
    /// Time zone used to display event times.
    /// </summary>
    private static readonly TimeZoneInfo ClubTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");

    // Sample data for display.
    private static readonly EventCard[] SampleEvents =
    [
        new(1, "Sample Event 1", new DateTime(2026, 10, 15, 23, 0, 0, DateTimeKind.Utc), "Short description of the event."),
        new(2, "Sample Event 2", new DateTime(2026, 10, 22, 23, 0, 0, DateTimeKind.Utc), "Short description of the event."),
        new(3, "Sample Event 3", new DateTime(2026, 10, 29, 23, 0, 0, DateTimeKind.Utc), "Short description of the event."),
    ];

    private static readonly NewsItem[] SampleNews =
    [
        new("News Item 1", "Short summary of the news."),
        new("News Item 2", "Short summary of the news."),
        new("News Item 3", "Short summary of the news."),
    ];

    /// <summary>
    /// Upcoming events, soonest first. Empty when there is nothing scheduled.
    /// </summary>
    public IReadOnlyList<EventCard> UpcomingEvents { get; private set; } = [];

    /// <summary>
    /// Club news items. Empty when there is no news yet.
    /// </summary>
    public IReadOnlyList<NewsItem> News { get; private set; } = [];

    /// <param name="empty">When true (/Events?empty=true), show the empty state instead of the sample data.</param>
    public void OnGet(bool empty = false)
    {
        if (empty)
        {
            return;
        }

        UpcomingEvents = SampleEvents.OrderBy(e => e.StartsAtUtc).ToList();
        News = SampleNews;
    }

    /// <summary>
    /// Formats a UTC start time in club time, for example "Thu, Oct 15 · 6:00 PM".
    /// </summary>
    public static string FormatClubTime(DateTime startsAtUtc)
    {
        var utc = DateTime.SpecifyKind(startsAtUtc, DateTimeKind.Utc);
        var clubTime = TimeZoneInfo.ConvertTimeFromUtc(utc, ClubTimeZone);
        return clubTime.ToString("ddd, MMM d '·' h:mm tt", CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// One event card on the Events home page.
/// </summary>
public record EventCard(int Id, string Name, DateTime StartsAtUtc, string Summary);

/// <summary>
/// One item in the News block on the Events home page.
/// </summary>
public record NewsItem(string Title, string Summary);
