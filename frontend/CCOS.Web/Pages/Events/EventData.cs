using System.Globalization;
using CCOS.Shared;
using CCOS.Web.Services;

namespace CCOS.Web.Pages.Events;

/// <summary>
/// One event as shown on the Events pages. Field names follow the Event entity
/// (see "Event, Ticket &amp; Venue Relational Schema" in docs). Filled from CCOS.Api by
/// EventMapping (CAR-50).
/// </summary>
/// <param name="VenueId">The event's venue (Event.VenueId).</param>
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
    int TicketCapacity,
    bool MembersOnly,
    bool IsActive,
    int VenueId,
    string VenueName,
    IReadOnlyList<TicketTypeInfo> TicketTypes,
    IReadOnlyList<string> Categories);

/// <summary>
/// One ticket type for an event, for example "General" at $5 with a $0 member price.
/// Matches the Product columns a TicketType uses: Name, Price, MemberPrice and IsActive.
/// MemberPrice is null when the visitor isn't a current member, so the API didn't send it (CAR-69).
/// </summary>
public record TicketTypeInfo(string Name, decimal Price, decimal? MemberPrice, bool IsActive = true);

/// <summary>
/// One venue an event can be held at. Matches Venue.VenueId and Venue.Name.
/// </summary>
public record VenueInfo(int VenueId, string Name);

/// <summary>
/// Event categories. Shown on the wireframes but not in the database yet, so sample data only.
/// </summary>
public static class SampleCategories
{
    /// <summary>The category names offered on the officer event forms.</summary>
    public static IReadOnlyList<string> All { get; } = ["Workshop", "Social"];
}

/// <summary>
/// Turns events from CCOS.Api into the shape the Events pages use (CAR-50).
/// </summary>
public static class EventMapping
{
    /// <summary>
    /// Copies an API event into an EventInfo. Categories aren't in the database yet, so none are shown.
    /// </summary>
    public static EventInfo ToEventInfo(EventDto dto) => new(
        dto.EventId,
        dto.Name,
        dto.Description,
        dto.StartsAtUtc,
        dto.EndsAtUtc,
        dto.TicketCapacity,
        dto.MembersOnly,
        dto.IsActive,
        dto.VenueId,
        dto.VenueName,
        dto.TicketTypes
            .Select(ticketType => new TicketTypeInfo(ticketType.Name, ticketType.Price, ticketType.MemberPrice, ticketType.IsActive))
            .ToList(),
        []);
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
    public static string? RegularPriceText(EventInfo eventInfo) =>
        PriceText(eventInfo.TicketTypes
            .Where(ticketType => ticketType.IsActive)
            .Select(ticketType => ticketType.Price)
            .ToList());

    /// <summary>
    /// Lowest member price across the event's active ticket types, with "from" when the
    /// ticket types have different member prices. Null when no ticket types are on sale, or when
    /// the visitor can't see member prices.
    /// </summary>
    public static string? MemberPriceText(EventInfo eventInfo) =>
        PriceText(eventInfo.TicketTypes
            .Where(ticketType => ticketType.IsActive && ticketType.MemberPrice is not null)
            .Select(ticketType => ticketType.MemberPrice!.Value)
            .ToList());

    /// <summary>
    /// The prices an event shows, on its card (CAR-76) and on its page. Members-only events show
    /// just the member price, since only members can buy tickets. Other events show the regular
    /// price, plus the member price only when it's different.
    /// </summary>
    /// <param name="eventInfo">The event.</param>
    /// <param name="large">True on the event page, false on a card.</param>
    /// <param name="regularLabel">Optional label before a regular price shown on its own.</param>
    public static PriceTag ForEvent(EventInfo eventInfo, bool large = false, string? regularLabel = null)
    {
        var regular = RegularPriceText(eventInfo);
        var member = MemberPriceText(eventInfo);

        return eventInfo.MembersOnly
            ? new PriceTag(null, member, large, regularLabel)
            : new PriceTag(regular, member != regular ? member : null, large, regularLabel);
    }

    /// <summary>
    /// True when tickets are on sale but the API left out the member price, because the visitor
    /// isn't a current member (CAR-69). The event page then invites them to log in.
    /// </summary>
    public static bool MemberPriceHidden(EventInfo eventInfo) =>
        eventInfo.TicketTypes.Any(ticketType => ticketType.IsActive && ticketType.MemberPrice is null);

    /// <summary>
    /// Shared price wording: nothing when there are no prices, the price when they all match,
    /// or "from" plus the lowest price when they differ.
    /// </summary>
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
    /// Converts a club-time date and time (what an officer types in the form) to UTC for saving.
    /// Returns null when that time doesn't exist, which happens once a year when the clocks
    /// spring forward (for example 2:30 AM).
    /// </summary>
    public static DateTime? ToUtc(DateOnly date, TimeOnly time)
    {
        var clubTime = date.ToDateTime(time, DateTimeKind.Unspecified);

        if (ClubTimeZone.IsInvalidTime(clubTime))
        {
            return null;
        }

        return TimeZoneInfo.ConvertTimeToUtc(clubTime, ClubTimeZone);
    }

    /// <summary>
    /// Short start time for event cards, for example "Thu, Oct 15 · 6:00 PM".
    /// </summary>
    public static string FormatCard(DateTime startsAtUtc) =>
        ToClubTime(startsAtUtc).ToString("ddd, MMM d '·' h:mm tt", CultureInfo.InvariantCulture);

    /// <summary>
    /// Start and end time for an event card (CAR-76), for example "5:00 PM – 7:00 PM". When the
    /// event ends on a later day, the end includes its date: "9:00 PM – Nov 21, 1:00 AM".
    /// </summary>
    public static string FormatTimeRange(DateTime startsAtUtc, DateTime endsAtUtc)
    {
        var start = ToClubTime(startsAtUtc);
        var end = ToClubTime(endsAtUtc);
        var culture = CultureInfo.InvariantCulture;
        return start.Date == end.Date
            ? $"{start.ToString("h:mm tt", culture)} – {end.ToString("h:mm tt", culture)}"
            : $"{start.ToString("h:mm tt", culture)} – {end.ToString("MMM d, h:mm tt", culture)}";
    }

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
