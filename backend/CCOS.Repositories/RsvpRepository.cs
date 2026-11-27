using CCOS.Data;
using CCOS.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CCOS.Repositories;

/// <summary>
/// RSVPs (CAR-94). An RSVP is one free ticket reserved through a normal order, so it holds a seat
/// and counts against the event's capacity like any ticket (the order code checks seats, members-only
/// events and start times). Cancel RSVP gives the free seats back and marks the tickets canceled.
/// Paid tickets aren't RSVPs: the website puts those in the cart instead.
/// </summary>
public sealed class RsvpRepository(AppDbContext db, OrderRepository orders)
{
    /// <summary>The status an order gets when every ticket on it was canceled.</summary>
    public const string CanceledStatus = "Canceled";

    /// <summary>How many tickets a customer holds for an event, and how many of those are free RSVPs.</summary>
    /// <param name="customerId">Whose tickets.</param>
    /// <param name="eventId">Which event.</param>
    /// <param name="cancellationToken">Lets the caller stop the work early.</param>
    public async Task<RsvpStatus> GetStatusAsync(int customerId, int eventId, CancellationToken cancellationToken = default)
    {
        List<decimal> prices = await LiveTickets(customerId, eventId)
            .AsNoTracking()
            .Select(registration => registration.OrderLine.UnitPrice)
            .ToListAsync(cancellationToken);

        return new RsvpStatus(prices.Count, prices.Count(price => price == 0m));
    }

    /// <summary>
    /// RSVPs a customer to an event: places an order for one ticket of a type that's free for them.
    /// Someone already going isn't booked twice.
    /// </summary>
    /// <param name="customerId">Who is going.</param>
    /// <param name="isCurrentMember">True for a current member (member prices, members-only events).</param>
    /// <param name="eventId">Which event.</param>
    /// <param name="cancellationToken">Lets the caller stop the work early.</param>
    public async Task<RsvpResult> RsvpAsync(int customerId, bool isCurrentMember, int eventId, CancellationToken cancellationToken = default)
    {
        RsvpStatus status = await GetStatusAsync(customerId, eventId, cancellationToken);
        if (status.Tickets > 0)
        {
            return new RsvpResult(RsvpOutcome.AlreadyGoing, null);
        }

        Event? @event = await db.Events
            .AsNoTracking()
            .Include(e => e.TicketTypes)
            .FirstOrDefaultAsync(e => e.EventId == eventId && e.IsActive, cancellationToken);
        if (@event is null)
        {
            return new RsvpResult(RsvpOutcome.NotFound, "That event could not be found.");
        }

        // The same price rule as orders: the member price for current members when it's lower.
        TicketType? free = @event.TicketTypes
            .Where(ticketType => ticketType.IsActive)
            .FirstOrDefault(ticketType => PriceFor(ticketType, isCurrentMember) == 0m);
        if (free is null)
        {
            return new RsvpResult(RsvpOutcome.NotFree, $"{@event.Name} isn't free, so add a ticket to your cart instead.");
        }

        PlaceOrderResult placed = await orders.PlaceOrderAsync(
            customerId,
            isCurrentMember,
            [new OrderRequestLine(free.ProductId, 1)],
            cancellationToken);

        return placed.Outcome switch
        {
            PlaceOrderOutcome.Placed => new RsvpResult(RsvpOutcome.Going, null),
            PlaceOrderOutcome.MembersOnly => new RsvpResult(RsvpOutcome.MembersOnly, placed.Message),
            _ => new RsvpResult(RsvpOutcome.Unavailable, placed.Message),
        };
    }

    /// <summary>
    /// Cancels a customer's free RSVP tickets for an event and gives the seats back, in one
    /// transaction. An order whose tickets are all canceled (and has no merch) is marked Canceled.
    /// Paid tickets are left alone; officers handle those (CAR-96).
    /// </summary>
    /// <param name="customerId">Whose RSVP.</param>
    /// <param name="eventId">Which event.</param>
    /// <param name="cancellationToken">Lets the caller stop the work early.</param>
    /// <returns>How many tickets were canceled. 0 when there was no RSVP to cancel.</returns>
    /// <exception cref="InvalidOperationException">The event's sold count is lower than the tickets being canceled.</exception>
    public async Task<int> CancelAsync(int customerId, int eventId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        List<TicketRegistration> rsvps = await LiveTickets(customerId, eventId)
            .Where(registration => registration.OrderLine.UnitPrice == 0m)
            .Include(registration => registration.OrderLine)
                .ThenInclude(line => line.Order)
                    .ThenInclude(order => order.OrderLines)
                        .ThenInclude(line => line.TicketRegistrations)
            .ToListAsync(cancellationToken);

        if (rsvps.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return 0;
        }

        // Give the seats back. The ">=" check keeps the count from going below zero.
        int seats = rsvps.Count;
        int updated = await db.Events
            .Where(e => e.EventId == eventId && e.TicketsSold >= seats)
            .ExecuteUpdateAsync(setters => setters.SetProperty(e => e.TicketsSold, e => e.TicketsSold - seats), cancellationToken);
        if (updated != 1)
        {
            throw new InvalidOperationException($"Event {eventId} has fewer sold tickets than the RSVPs being canceled.");
        }

        DateTime now = DateTime.UtcNow;
        foreach (TicketRegistration rsvp in rsvps)
        {
            rsvp.Status = RegistrationStatus.Canceled;
            rsvp.CanceledAtUtc = now;
        }

        // An RSVP order is just the ticket: once nothing on it is still good, the order is canceled too.
        foreach (Order order in rsvps.Select(rsvp => rsvp.OrderLine.Order).Distinct())
        {
            bool allTicketsCanceled = order.OrderLines.All(line =>
                line.TicketRegistrations.Count > 0
                && line.TicketRegistrations.All(registration => registration.Status == RegistrationStatus.Canceled));
            if (allTicketsCanceled)
            {
                order.Status = CanceledStatus;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return seats;
    }

    private static decimal PriceFor(TicketType ticketType, bool isCurrentMember) =>
        isCurrentMember && ticketType.MemberPrice < ticketType.Price ? ticketType.MemberPrice : ticketType.Price;

    // The customer's tickets for the event that are still good.
    private IQueryable<TicketRegistration> LiveTickets(int customerId, int eventId) =>
        db.TicketRegistrations.Where(registration =>
            registration.EventId == eventId
            && registration.Status == RegistrationStatus.Purchased
            && registration.OrderLine.Order.CustomerId == customerId);
}

/// <summary>How many tickets a customer holds for an event (CAR-94).</summary>
/// <param name="Tickets">Every ticket that's still good.</param>
/// <param name="FreeTickets">How many of them are free RSVPs.</param>
public sealed record RsvpStatus(int Tickets, int FreeTickets);

/// <summary>What happened when someone RSVPed (CAR-94).</summary>
/// <param name="Outcome">The result.</param>
/// <param name="Message">A sentence for the person when it didn't work, or null.</param>
public sealed record RsvpResult(RsvpOutcome Outcome, string? Message);

/// <summary>The possible results of an RSVP.</summary>
public enum RsvpOutcome
{
    /// <summary>One free ticket was reserved.</summary>
    Going,

    /// <summary>They already hold a ticket, so nothing new was booked.</summary>
    AlreadyGoing,

    /// <summary>No active event with that id.</summary>
    NotFound,

    /// <summary>No ticket type is free for them; the website adds a ticket to the cart instead.</summary>
    NotFree,

    /// <summary>A members-only event and they aren't a current member.</summary>
    MembersOnly,

    /// <summary>Sold out, or ticket sales have closed.</summary>
    Unavailable,
}
