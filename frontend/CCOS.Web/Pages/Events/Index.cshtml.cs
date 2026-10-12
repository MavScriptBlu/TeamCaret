using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Events;

/// <summary>
/// Events home page (/Events): upcoming events and news.
/// Shows sample data for now. /Events?empty=true shows the empty state.
/// </summary>
public class IndexModel : PageModel
{
    // Sample data for display.
    private static readonly NewsItem[] SampleNews =
    [
        new("News Item 1", "Short summary of the news."),
        new("News Item 2", "Short summary of the news."),
        new("News Item 3", "Short summary of the news."),
    ];

    /// <summary>
    /// Active events starting from now on, soonest first. Empty when there is nothing scheduled.
    /// </summary>
    public IReadOnlyList<EventInfo> UpcomingEvents { get; private set; } = [];

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

        UpcomingEvents = SampleEvents.Upcoming();
        News = SampleNews;
    }
}

/// <summary>
/// One item in the News block on the Events home page.
/// </summary>
public record NewsItem(string Title, string Summary);
