using System.Net;
using System.Text.Json;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Events;

/// <summary>
/// Event page (/Events/Details/{id}): image, name, date and time, venue, categories,
/// ticket price, RSVP and description. Loads the event from CCOS.Api (CAR-50). The officer
/// menu can remove the event (CAR-56): it is only hidden (soft-deleted) and can be restored.
/// </summary>
public class DetailsModel(EventsApiClient eventsApi, ILogger<DetailsModel> logger) : PageModel
{
    private static readonly Action<ILogger, int, Exception?> EventRemoveTimeoutLog =
        LoggerMessage.Define<int>(LogLevel.Error, new EventId(1, nameof(EventRemoveTimeoutLog)),
            "The event API request timed out while removing event {EventId}");

    private static readonly Action<ILogger, int, Exception?> EventRemoveUnreachableLog =
        LoggerMessage.Define<int>(LogLevel.Error, new EventId(2, nameof(EventRemoveUnreachableLog)),
            "The event API could not be reached while removing event {EventId}");

    private static readonly Action<ILogger, int?, Exception?> EventLoadFailedLog =
        LoggerMessage.Define<int?>(LogLevel.Error, new EventId(3, nameof(EventLoadFailedLog)),
            "Couldn't load event {EventId} from CCOS.Api.");

    private static readonly Action<ILogger, int, HttpStatusCode, Exception?> EventRemoveFailedLog =
        LoggerMessage.Define<int, HttpStatusCode>(LogLevel.Error, new EventId(4, nameof(EventRemoveFailedLog)),
            "Failed to remove event {EventId}. Status code: {StatusCode}");

    /// <summary>
    /// The event to show, or null when no active event matches the id.
    /// </summary>
    public EventInfo? Event { get; private set; }

    /// <summary>
    /// Regular ticket price, for example "$10.00", "from $5.00" or "Free".
    /// Null for members-only events and when no tickets are on sale.
    /// </summary>
    public string? PriceText { get; private set; }

    /// <summary>
    /// Member ticket price. Shown when it differs from the regular price,
    /// and always for members-only events. Null when no tickets are on sale.
    /// </summary>
    public string? MemberPriceText { get; private set; }

    /// <summary>
    /// True when there is a member price the visitor can't see (not a current member, CAR-69).
    /// The page shows "log in as a member to see it" instead.
    /// </summary>
    public bool MemberPriceHidden { get; private set; }

    /// <summary>
    /// True when the API couldn't be reached, so the page shows an error message instead.
    /// </summary>
    public bool LoadFailed { get; private set; }

    /// <summary>The ticket types the visitor can add to the cart (CAR-92). Empty when they can't buy tickets.</summary>
    public IReadOnlyList<TicketOption> TicketOptions { get; private set; } = [];

    /// <summary>How many tickets for this event are already in the cart.</summary>
    public int TicketsInCart { get; private set; }

    /// <summary>The most tickets the visitor can still add (the seats left, less what's in their cart).</summary>
    public int MaxMoreTickets { get; private set; }

    /// <summary>
    /// Why tickets can't be added to the cart, for example "This event has ended.", or null when they can.
    /// </summary>
    public string? NoTicketsReason { get; private set; }

    /// <summary>True when the page shows the ticket Add to cart form.</summary>
    public bool CanAddTickets => NoTicketsReason is null && TicketOptions.Count > 0;

    /// <summary>
    /// Loads the event and works out the price text to show. Sends a 404 when the id is
    /// missing or no active event matches, and shows an error message when the API fails.
    /// </summary>
    /// <param name="id">The event id from the URL.</param>
    public async Task OnGetAsync(int? id, CancellationToken cancellationToken)
    {
        try
        {
            var dto = id is int eventId ? await eventsApi.GetAsync(eventId, cancellationToken) : null;
            Event = dto is null ? null : EventMapping.ToEventInfo(dto);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            EventLoadFailedLog(logger, id, ex);
            LoadFailed = true;
            return;
        }

        // Unknown or canceled event: send a 404. The page shows its "not found" message.
        if (Event is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // Same rule as the event cards on /Events (CAR-76): members-only events show just the
        // member price; other events show the regular price, plus the member price when it's different.
        var prices = TicketPrices.ForEvent(Event);
        PriceText = prices.Regular;
        MemberPriceText = prices.Member;
        MemberPriceHidden = TicketPrices.MemberPriceHidden(Event);

        // Ticket Add to cart (CAR-92).
        var cart = HttpContext.GetCart();
        TicketsInCart = cart.TicketsFor(Event.EventId);
        MaxMoreTickets = Math.Max(0, Event.TicketCapacity - TicketsInCart);
        NoTicketsReason = WhyNoTickets(Event, User.IsCurrentMember(), DateTime.UtcNow)
            ?? (MaxMoreTickets == 0 ? "Your cart already has all the seats left for this event." : null);
        if (NoTicketsReason is null)
        {
            TicketOptions = Event.TicketTypes
                .Where(ticketType => ticketType.IsActive)
                .Select(ticketType => new TicketOption(
                    ticketType.TicketTypeId,
                    $"{ticketType.Name} · {TicketPrices.Format(Cart.PriceYouPay(ticketType.Price, ticketType.MemberPrice))}"))
                .ToList();
        }
    }

    /// <summary>
    /// The Add to cart button for tickets (CAR-92). Loads the event again, so the rules are checked
    /// with fresh data: tickets on sale, the event hasn't ended, members-only events need a current
    /// member, and the cart never holds more tickets than the event has seats. Then comes back to
    /// the event page with a message.
    /// </summary>
    /// <param name="id">The event id from the URL.</param>
    /// <param name="ticketTypeId">The ticket type picked (the only one when there's just one).</param>
    /// <param name="quantity">How many tickets, from the Qty box.</param>
    public async Task<IActionResult> OnPostAddToCartAsync(int? id, int ticketTypeId, int quantity, CancellationToken cancellationToken)
    {
        if (id is not int eventId)
        {
            return RedirectToPage("/Events/Index");
        }

        EventInfo? eventInfo;
        try
        {
            var dto = await eventsApi.GetAsync(eventId, cancellationToken);
            eventInfo = dto is null ? null : EventMapping.ToEventInfo(dto);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            EventLoadFailedLog(logger, eventId, ex);
            TempData[CartMessages.ErrorKey] = CartMessages.TryAgainLater;
            return RedirectToPage("/Events/Details", new { id = eventId });
        }

        if (eventInfo is null || !eventInfo.IsActive)
        {
            TempData["EventError"] = "That event could not be found. It may have been removed.";
            return RedirectToPage("/Events/Index");
        }

        var backToEvent = RedirectToPage("/Events/Details", new { id = eventId });

        var reason = WhyNoTickets(eventInfo, User.IsCurrentMember(), DateTime.UtcNow);
        if (reason is not null)
        {
            TempData[CartMessages.ErrorKey] = reason;
            return backToEvent;
        }

        var ticketType = eventInfo.TicketTypes.FirstOrDefault(type => type.IsActive && type.TicketTypeId == ticketTypeId);
        if (ticketType is null)
        {
            TempData[CartMessages.ErrorKey] = "Choose a ticket type.";
            return backToEvent;
        }

        if (quantity < 1)
        {
            TempData[CartMessages.ErrorKey] = CartMessages.PickAQuantity;
            return backToEvent;
        }

        var cart = HttpContext.GetCart();
        // Seats left is the event's capacity for now. Tickets that have been ordered will count
        // against it once orders exist (CAR-91).
        var max = cart.MaxTickets(eventId, ticketTypeId, eventInfo.TicketCapacity);
        if (max == 0 && cart.TicketsFor(eventId) > 0)
        {
            TempData[CartMessages.ErrorKey] = "Your cart already has all the seats left for this event.";
            return backToEvent;
        }

        var result = cart.Add(CartItemKind.Ticket, ticketTypeId, eventId, quantity, max);
        if (result != CartAddResult.NoneAvailable)
        {
            HttpContext.SaveCart(cart);
        }

        CartMessages.Put(TempData, result, $"{eventInfo.Name} – {ticketType.Name} ticket", quantity,
            cart.QuantityOf(CartItemKind.Ticket, ticketTypeId), max);
        return backToEvent;
    }

    /// <summary>
    /// Why tickets for this event can't go in the cart, or null when they can (CAR-92). Shared by
    /// the event page and the Cart page.
    /// </summary>
    /// <param name="eventInfo">The event.</param>
    /// <param name="isCurrentMember">True when the visitor is a current member.</param>
    /// <param name="nowUtc">The time now, in UTC.</param>
    public static string? WhyNoTickets(EventInfo eventInfo, bool isCurrentMember, DateTime nowUtc)
    {
        if (!eventInfo.TicketTypes.Any(ticketType => ticketType.IsActive))
        {
            return "Tickets aren't on sale yet.";
        }

        if (eventInfo.EndsAtUtc <= nowUtc)
        {
            return "This event has ended.";
        }

        if (eventInfo.MembersOnly && !isCurrentMember)
        {
            return "Members only: log in as a current member to get tickets.";
        }

        if (eventInfo.TicketCapacity <= 0)
        {
            return "Sold out.";
        }

        return null;
    }

    /// <summary>
    /// One choice in the ticket type dropdown, for example "General · $5.00".
    /// </summary>
    /// <param name="TicketTypeId">The ticket type id the form sends.</param>
    /// <param name="Label">The ticket name and the price this visitor pays.</param>
    public sealed record TicketOption(int TicketTypeId, string Label);

    /// <summary>
    /// The Remove event button (after the officer says yes in the dialog). Soft-removes the event
    /// through DELETE /api/events/{id}, then goes to the Events page. The row stays in the database.
    /// </summary>
    /// <param name="id">The event id from the URL.</param>
    public async Task<IActionResult> OnPostRemoveAsync(int? id, CancellationToken cancellationToken)
    {
        if (id is not int eventId)
        {
            TempData["EventError"] = "The event could not be identified. Please try again.";
            return RedirectToPage("/Events/Index");
        }

        try
        {
            using var response = await eventsApi.DeleteAsync(eventId, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                TempData["EventError"] = "That event could not be found. It may already be gone.";
                return RedirectToPage("/Events/Index");
            }

            if (!response.IsSuccessStatusCode)
            {
                EventRemoveFailedLog(logger, eventId, response.StatusCode, null);
                TempData["EventError"] = "The event could not be removed. Please try again.";
                return RedirectToPage("/Events/Details", new { id = eventId });
            }
        }
        catch (HttpRequestException ex)
        {
            EventRemoveUnreachableLog(logger, eventId, ex);
            TempData["EventError"] = "The event API could not be reached. Please try again later.";
            return RedirectToPage("/Events/Details", new { id = eventId });
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            EventRemoveTimeoutLog(logger, eventId, ex);
            TempData["EventError"] = "The event API request timed out. Please try again later.";
            return RedirectToPage("/Events/Details", new { id = eventId });
        }

        TempData["EventRemoved"] = "Event removed. You can restore it from the officer event list.";
        return RedirectToPage("/Events/Index");
    }
}
