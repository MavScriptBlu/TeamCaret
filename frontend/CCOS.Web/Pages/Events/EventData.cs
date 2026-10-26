using System.Globalization;
using CCOS.Shared;

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
/// </summary>
public record TicketTypeInfo(string Name, decimal Price, decimal MemberPrice, bool IsActive = true);

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
    /// ticket types have different member prices. Null when no ticket types are on sale.
    /// </summary>
    public static string? MemberPriceText(EventInfo eventInfo) =>
        PriceText(eventInfo.TicketTypes
            .Where(ticketType => ticketType.IsActive)
            .Select(ticketType => ticketType.MemberPrice)
            .ToList());

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
