using System.Globalization;
using System.Text.Json;
using CCOS.Shared;
using CCOS.Web.Pages.Events;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages;

/// <summary>
/// My orders (/MyOrders, CAR-95): the signed-in person's orders, newest first (number, date,
/// items, total, status), plus their upcoming RSVPs and tickets with each event's date, time and
/// venue. Signing in is required; a visitor who isn't signed in goes to Log in and comes back.
/// </summary>
[Authorize]
public class MyOrdersModel(MyOrdersApiClient ordersApi, EventsApiClient eventsApi, ILogger<MyOrdersModel> logger) : PageModel
{
    private const string CanceledStatus = "Canceled";

    /// <summary>The person's orders, newest first.</summary>
    public IReadOnlyList<OrderSummary> Orders { get; private set; } = [];

    /// <summary>Upcoming events the person has tickets or an RSVP for, soonest first.</summary>
    public IReadOnlyList<UpcomingEvent> UpcomingEvents { get; private set; } = [];

    /// <summary>True when the orders couldn't be loaded, so the page shows an error message instead.</summary>
    public bool LoadFailed { get; private set; }

    /// <summary>
    /// Loads the orders, then the events their tickets are for. Past and removed events are left
    /// out of the upcoming list (the API's public event lookup doesn't return them).
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var orders = await ordersApi.GetMineAsync(cancellationToken);
            Orders = orders.Select(ToSummary).ToList();
            UpcomingEvents = await LoadUpcomingAsync(orders, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Couldn't load My orders from CCOS.Api.");
            LoadFailed = true;
        }
    }

    /// <summary>
    /// What a status means to the person, for example "Pending (pay in person)". Officers mark
    /// orders paid, picked up or canceled (CAR-96); anything new shows as the API sends it.
    /// </summary>
    /// <param name="status">The order's status from the API.</param>
    public static string StatusText(string status) => status switch
    {
        "Pending" => "Pending (pay in person)",
        "Paid" => "Paid",
        "PickedUp" or "Picked up" or "Fulfilled" => "Picked up",
        CanceledStatus => "Canceled",
        _ => status,
    };

    // A short word for the status tag's color in orders.css.
    private static string StatusKey(string status) => StatusText(status) switch
    {
        "Pending (pay in person)" => "pending",
        "Paid" => "paid",
        "Picked up" => "pickedup",
        "Canceled" => "canceled",
        _ => "other",
    };

    // The event cards for every upcoming event with tickets in an order that isn't canceled.
    private async Task<IReadOnlyList<UpcomingEvent>> LoadUpcomingAsync(IReadOnlyList<OrderDto> orders, CancellationToken cancellationToken)
    {
        var ticketsByEvent = orders
            .Where(order => order.Status != CanceledStatus)
            .SelectMany(order => order.Lines)
            .Where(line => line.EventId is not null)
            .GroupBy(line => line.EventId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity));

        var upcoming = new List<UpcomingEvent>();
        foreach (var (eventId, tickets) in ticketsByEvent)
        {
            var dto = await eventsApi.GetAsync(eventId, cancellationToken);
            if (dto is null || !dto.IsActive || dto.EndsAtUtc <= DateTime.UtcNow)
            {
                continue;
            }

            upcoming.Add(new UpcomingEvent(
                dto.EventId,
                dto.Name,
                dto.StartsAtUtc,
                ClubTime.FormatRange(dto.StartsAtUtc, dto.EndsAtUtc),
                dto.VenueName,
                tickets));
        }

        return upcoming.OrderBy(item => item.StartsAtUtc).ToList();
    }

    private static OrderSummary ToSummary(OrderDto order) => new(
        order.OrderId,
        ClubTime.ToClubTime(order.OrderDate).ToString("MMM d, yyyy · h:mm tt", CultureInfo.InvariantCulture),
        StatusText(order.Status),
        StatusKey(order.Status),
        Money.Format(order.TotalAmount),
        order.Lines.Select(line => new OrderItem(line.Name, line.Quantity, Money.Format(line.LineTotal), line.EventId is not null)).ToList());

    /// <summary>One order on the page.</summary>
    /// <param name="OrderId">The order number.</param>
    /// <param name="DateText">When it was placed, in club time, for example "Nov 27, 2026 · 8:15 AM".</param>
    /// <param name="StatusText">The status in plain words.</param>
    /// <param name="StatusKey">The status as a lowercase word for the status tag's color, for example "pending".</param>
    /// <param name="TotalText">The order total, for example "$45.00".</param>
    /// <param name="Items">What was ordered.</param>
    public sealed record OrderSummary(int OrderId, string DateText, string StatusText, string StatusKey, string TotalText, IReadOnlyList<OrderItem> Items);

    /// <summary>One line of an order.</summary>
    /// <param name="Name">The product or ticket name at the time of the order.</param>
    /// <param name="Quantity">How many.</param>
    /// <param name="LineTotalText">What the line cost.</param>
    /// <param name="IsTicket">True for event tickets.</param>
    public sealed record OrderItem(string Name, int Quantity, string LineTotalText, bool IsTicket);

    /// <summary>An upcoming event the person has tickets or an RSVP for.</summary>
    /// <param name="EventId">The event, for the link to its page (where Cancel RSVP will live, CAR-94).</param>
    /// <param name="Name">The event's name.</param>
    /// <param name="StartsAtUtc">When it starts, for sorting.</param>
    /// <param name="WhenText">The date and time in club time.</param>
    /// <param name="VenueName">Where it is.</param>
    /// <param name="Tickets">How many tickets the person holds across their orders.</param>
    public sealed record UpcomingEvent(int EventId, string Name, DateTime StartsAtUtc, string WhenText, string VenueName, int Tickets);
}
