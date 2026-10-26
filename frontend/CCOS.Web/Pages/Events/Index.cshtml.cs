using System.Text.Json;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Events;

/// <summary>
/// Events home page (/Events): upcoming events from CCOS.Api (CAR-50) and club news.
/// /Events?empty=true shows the empty state.
/// </summary>
public class IndexModel(EventsApiClient eventsApi, ILogger<IndexModel> logger) : PageModel
{
    // News isn't in the database yet, so it stays sample data.
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

    /// <summary>
    /// True when the API couldn't be reached, so the page shows an error message instead.
    /// </summary>
    public bool LoadFailed { get; private set; }

    /// <summary>
    /// Loads the upcoming events from the API, plus the news.
    /// </summary>
    /// <param name="empty">When true (/Events?empty=true), show the empty state instead.</param>
    public async Task OnGetAsync(bool empty = false, CancellationToken cancellationToken = default)
    {
        // Leave both lists empty so the page's "nothing yet" message shows.
        if (empty)
        {
            return;
        }

        News = SampleNews;

        try
        {
            var events = await eventsApi.ListUpcomingAsync(cancellationToken);
            UpcomingEvents = events.Select(EventMapping.ToEventInfo).ToList();
        }
        // Same handling as the product pages: a timeout counts as a failure, but a visitor
        // closing the page (cancellation) is not an error worth logging.
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Couldn't load upcoming events from CCOS.Api.");
            LoadFailed = true;
        }
    }
}

/// <summary>
/// One item in the News block on the Events home page.
/// </summary>
public record NewsItem(string Title, string Summary);
