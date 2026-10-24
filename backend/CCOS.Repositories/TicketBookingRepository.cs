using CCOS.Data;
using CCOS.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CCOS.Repositories;

/// <summary>
/// Handles the ticket counts on events: holding seats when an order is placed and giving
/// them back when an order is canceled.
///
/// Every change to Events.TicketsSold goes through a single conditional UPDATE in the database,
/// so two people buying the last ticket at the same time can't both get it (CAR-30).
/// </summary>
public sealed class TicketBookingRepository(AppDbContext db)
{
    // TODO: Add scheduled/admin TicketsSold consistency monitoring in its follow-up, outside the booking path.
    // TODO: Order placement must validate membership and ticket activity, add registrations, and own this transaction.

    /// <summary>
    /// Holds seats for one event. A shortcut for ReserveTicketsForOrderAsync with a single event.
    /// Must be called inside the order transaction.
    /// </summary>
    /// <param name="eventId">The event to hold seats on.</param>
    /// <param name="quantity">How many seats to hold.</param>
    /// <param name="cancellationToken">Lets the caller stop the work early.</param>
    /// <exception cref="SoldOutException">The event doesn't have enough seats left.</exception>
    public async Task ReserveSeatsAsync(
        int eventId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        await ReserveTicketsForOrderAsync(
            [new EventTicketReservation(eventId, quantity)],
            cancellationToken);
    }

    /// <summary>
    /// Holds seats for every ticket line in an order. It is all or nothing: if any one event is
    /// sold out, SoldOutException is thrown and the caller rolls back the whole order transaction.
    /// </summary>
    /// <param name="reservations">The events and ticket counts the order needs.</param>
    /// <param name="cancellationToken">Lets the caller stop the work early.</param>
    /// <exception cref="ArgumentException">The list is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An event id or quantity is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">There is no open order transaction.</exception>
    /// <exception cref="SoldOutException">An event doesn't have enough seats left.</exception>
    public async Task ReserveTicketsForOrderAsync(

        IEnumerable<EventTicketReservation> reservations,
        CancellationToken cancellationToken = default)
    {
        // Combine lines for the same event into one total, then sort by event id. Always taking
        // events in the same order means two orders can't lock each other out (deadlock).
        var groupedReservations = reservations
            .GroupBy(reservation => reservation.EventId)
            .Select(group => new EventTicketReservation(
                group.Key,
                group.Aggregate(0, (total, reservation) => checked(total + reservation.Quantity))))
            .OrderBy(reservation => reservation.EventId)
            .ToList();

        // Check the request before touching the database.
        if (groupedReservations.Count == 0)
        {
            throw new ArgumentException("At least one event reservation is required.", nameof(reservations));
        }

        if (groupedReservations.Any(reservation => reservation.EventId <= 0 || reservation.Quantity <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(reservations), "Event IDs and quantities must be positive.");
        }

        // The caller owns the transaction so the seats and the order succeed or fail together.
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Ticket reservations must run inside the order transaction.");
        }

        foreach (var reservation in groupedReservations)
        {
            // One UPDATE that only runs if the event is active and has room. The room check and
            // the increase happen together in the database, so there is no gap for another buyer.
            // It returns how many rows changed: 0 means the event was full, inactive, or missing.
            var reserved = await db.Events
                .Where(@event =>
                    @event.EventId == reservation.EventId
                    && @event.IsActive
                    && @event.TicketsSold + (long)reservation.Quantity <= @event.TicketCapacity)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        @event => @event.TicketsSold,
                        @event => @event.TicketsSold + reservation.Quantity),
                    cancellationToken);

            if (reserved == 0)
            {
                throw new SoldOutException(reservation.EventId);
            }
        }
    }

    /// <summary>
    /// Cancels every purchased ticket on an order and gives the seats back to the events.
    /// Runs in its own transaction, so either everything is canceled or nothing is.
    /// </summary>
    /// <param name="orderId">The order whose tickets should be canceled.</param>
    /// <param name="canceledAtUtc">When it was canceled. Defaults to now.</param>
    /// <param name="cancellationToken">Lets the caller stop the work early.</param>
    /// <returns>How many tickets were canceled. 0 if the order had none left to cancel.</returns>
    /// <exception cref="InvalidOperationException">
    /// An event has fewer tickets sold than the number being canceled, which means the counts are out of sync.
    /// </exception>
    public async Task<int> CancelOrderRegistrationsAsync(
        int orderId,
        DateTime? canceledAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        // The database stores UTC only, so convert whatever time we were given.
        // A time with no zone attached is treated as already being UTC.
        var canceledAt = canceledAtUtc ?? DateTime.UtcNow;
        canceledAt = canceledAt.Kind switch
        {
            DateTimeKind.Utc => canceledAt,
            DateTimeKind.Local => canceledAt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(canceledAt, DateTimeKind.Utc)
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Find which events this order has live tickets for, sorted by event id so the
        // locks are taken in the same order every time (same deadlock guard as reserving).
        var eventRegistrations = await db.TicketRegistrations
            .Where(registration =>
                registration.OrderLine.OrderId == orderId
                && registration.Status == RegistrationStatus.Purchased)
            .GroupBy(registration => registration.EventId)
            .Select(group => group.Key)
            .OrderBy(eventId => eventId)
            .ToListAsync(cancellationToken);

        var totalCanceled = 0;
        foreach (var eventId in eventRegistrations)
        {
            // Step 1: mark this event's purchased tickets as canceled.
            // Only "Purchased" rows match, so running this twice never cancels anything twice.
            var canceled = await db.TicketRegistrations
                .Where(registration =>
                    registration.EventId == eventId
                    && registration.OrderLine.OrderId == orderId
                    && registration.Status == RegistrationStatus.Purchased)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(registration => registration.Status, RegistrationStatus.Canceled)
                        .SetProperty(registration => registration.CanceledAtUtc, canceledAt),
                    cancellationToken);

            if (canceled == 0)
            {
                continue;
            }

            // Step 2: give those seats back. The TicketsSold >= canceled check keeps the count
            // from dropping below zero if the numbers were ever out of sync.
            var decremented = await db.Events
                .Where(@event =>
                    @event.EventId == eventId
                    && @event.TicketsSold >= canceled)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        @event => @event.TicketsSold,
                        @event => @event.TicketsSold - canceled),
                    cancellationToken);

            if (decremented == 0)
            {
                throw new InvalidOperationException(
                    $"Event {eventId} has fewer sold tickets than the registrations being canceled.");
            }

            totalCanceled += canceled;
        }

        // Only save the cancellations once every event has been updated.
        await transaction.CommitAsync(cancellationToken);
        return totalCanceled;
    }
}
