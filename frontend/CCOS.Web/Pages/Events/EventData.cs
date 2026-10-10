using System.Globalization;

namespace CCOS.Web.Pages.Events;

/// <summary>
/// One event as shown on the Events pages. Field names follow the Event entity
/// (see "Event, Ticket &amp; Venue Relational Schema" in docs), so swapping the sample
/// data for a database query later doesn't change the pages.
/// </summary>
/// <param name="VenueName">Venue.Name of the event's venue.</param>
/// <param name="TicketTypes">The event's ticket types (TicketType products).</param>
/// <param name="Categories">
/// Shown on the wireframe but not in the database yet, so this is sample data only.
/// </param>
public record EventInfo(
    int EventId,
    string Name,
    string? Description,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    bool MembersOnly,
    bool IsActive,
    string VenueName,
    IReadOnlyList<TicketTypeInfo> TicketTypes,
    IReadOnlyList<string> Categories);

/// <summary>
/// One ticket type for an event, for example "General" at $5 with a $0 member price.
/// Matches the Product columns a TicketType uses: Name, Price, MemberPrice and IsActive.
/// </summary>
public record TicketTypeInfo(string Name, decimal Price, decimal MemberPrice, bool IsActive = true);

/// <summary>
/// Sample events for display. Shared by the Events home page and the event page
/// so both always show the same events.
/// </summary>
public static class SampleEvents
{
    // Dates are set relative to today so the samples always stay upcoming.
    // 23:00 UTC is 6:00 PM in club time (5:00 PM outside daylight saving).
    /// <summary>
    /// All sample events.
    /// </summary>
    public static IReadOnlyList<EventInfo> All
    {
        get
        {
            var firstStartUtc =
                DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(7).AddHours(23), DateTimeKind.Utc);

            return
            [
                new(1, "Sample Event 1", "Description and further details about the event.",
                    firstStartUtc, firstStartUtc.AddHours(2),
                    MembersOnly: false, IsActive: true, "Sample Venue, Room 101",
                    [new("General", 10.00m, 5.00m)],
                    ["Workshop", "Social"]),
                new(2, "Sample Event 2", "Description and further details about the event.",
                    firstStartUtc.AddDays(7), firstStartUtc.AddDays(7).AddHours(2),
                    MembersOnly: false, IsActive: true, "Sample Venue, Room 102",
                    [new("General", 5.00m, 0.00m)],
                    ["Workshop"]),
                new(3, "Sample Event 3", "Description and further details about the event.",
                    firstStartUtc.AddDays(14), firstStartUtc.AddDays(14).AddHours(2),
                    MembersOnly: true, IsActive: true, "Sample Venue, Room 103",
                    [new("General", 0.00m, 0.00m)],
                    ["Social"]),
            ];
        }
    }

    /// <summary>
    /// Upcoming events for the public list: IsActive and starting from now on, soonest first.
    /// Same rule as the "Upcoming events" query in the schema doc.
    /// </summary>
    public static IReadOnlyList<EventInfo> Upcoming() =>
        All.Where(e => e.IsActive && e.StartsAtUtc >= DateTime.UtcNow)
           .OrderBy(e => e.StartsAtUtc)
           .ToList();

    /// <summary>
    /// Finds an event for the public event page, or null when no active event has that id.
    /// Canceled events (IsActive = false) are hidden, as on the public list.
    /// </summary>
    public static EventInfo? FindActive(int eventId) =>
        All.FirstOrDefault(e => e.EventId == eventId && e.IsActive);
}

/// <summary>
/// Ticket price text for the event page.
/// </summary>
public static class TicketPrices
{
    private static readonly CultureInfo Money = CultureInfo.GetCultureInfo("en-US");

    /// <summary>
    /// "Free" for $0, otherwise the price in dollars, for example "$10.00".
    /// </summary>
    public static string Format(decimal price) =>
        price == 0 ? "Free" : price.ToString("C", Money);

    /// <summary>
    /// Lowest regular price across the event's active ticket types, with "from" when the
    /// ticket types have different prices. Null when no ticket types are on sale.
    /// </summary>
    public static string? RegularPriceText(EventInfo ev) =>
        PriceText(ev.TicketTypes.Where(t => t.IsActive).Select(t => t.Price).ToList());

    /// <summary>
    /// Lowest member price across the event's active ticket types, with "from" when the
    /// ticket types have different member prices. Null when no ticket types are on sale.
    /// </summary>
    public static string? MemberPriceText(EventInfo ev) =>
        PriceText(ev.TicketTypes.Where(t => t.IsActive).Select(t => t.MemberPrice).ToList());

    private static string? PriceText(IReadOnlyList<decimal> prices)
    {
        if (prices.Count == 0)
        {
            return null;
        }

        var lowest = Format(prices.Min());
        return prices.Distinct().Count() > 1 ? $"from {lowest}" : lowest;
    }
}

/// <summary>
/// Formats event times in club time. Times are stored in UTC and shown in America/Chicago.
/// </summary>
public static class ClubTime
{
    private static readonly TimeZoneInfo ClubTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");

    /// <summary>
    /// Converts a UTC time to club time.
    /// </summary>
    public static DateTime ToClubTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), ClubTimeZone);

    /// <summary>
    /// Short start time for event cards, for example "Thu, Oct 15 · 6:00 PM".
    /// </summary>
    public static string FormatCard(DateTime startsAtUtc) =>
        ToClubTime(startsAtUtc).ToString("ddd, MMM d '·' h:mm tt", CultureInfo.InvariantCulture);

    /// <summary>
    /// Date and time range for the event page, for example "10/15/2026, 6:00 PM – 8:00 PM".
    /// </summary>
    public static string FormatRange(DateTime startsAtUtc, DateTime endsAtUtc)
    {
        var start = ToClubTime(startsAtUtc);
        var end = ToClubTime(endsAtUtc);
        var culture = CultureInfo.InvariantCulture;
        return start.Date == end.Date
            ? $"{start.ToString("MM/dd/yyyy", culture)}, {start.ToString("h:mm tt", culture)} – {end.ToString("h:mm tt", culture)}"
            : $"{start.ToString("MM/dd/yyyy, h:mm tt", culture)} – {end.ToString("MM/dd/yyyy, h:mm tt", culture)}";
    }
}
