using System.Globalization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Events;

/// <summary>
/// Events home page (/Events): the upcoming events grid and the club news block.
/// Uses sample data until the events API exists. /Events?empty=true shows the empty state.
/// </summary>
public class IndexModel : PageModel
{
    /// <summary>
    /// Club time zone. Event times are stored in UTC (CAR-30) and shown in club time.
    /// </summary>
    private static readonly TimeZoneInfo ClubTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");

    // Sample data until the events API is ready. Swap these for an API call in a later story.
    private static readonly EventCard[] SampleEvents =
    [
        new(1, "General Meeting", new DateTime(2026, 10, 15, 23, 0, 0, DateTimeKind.Utc), "Club updates, officer reports, and pizza."),
        new(2, "Intro to CTFs Workshop", new DateTime(2026, 10, 22, 23, 30, 0, DateTimeKind.Utc), "Hands-on capture-the-flag basics for beginners."),
        new(3, "Fall Social", new DateTime(2026, 11, 5, 23, 0, 0, DateTimeKind.Utc), "Games and snacks. Bring a friend."),
    ];

    private static readonly NewsItem[] SampleNews =
    [
        new("Officer elections", "Nominations open soon. Watch this space for dates."),
        new("New club hoodies", "Hoodies are now in the merch store."),
        new("Thanks for a great kickoff", "Thanks to everyone who came out to our first meeting."),
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
