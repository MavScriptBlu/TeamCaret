using CCOS.Data;
using CCOS.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CCOS.Repositories;

public sealed class TicketBookingRepository(AppDbContext db)
{
    // TODO: Add scheduled/admin TicketsSold consistency monitoring in its follow-up, outside the booking path.
    // TODO: Order placement must validate membership and ticket activity, add registrations, and own this transaction.
    public async Task ReserveSeatsAsync(
        int eventId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        await ReserveTicketsForOrderAsync(
            [new EventTicketReservation(eventId, quantity)],
            cancellationToken);
    }

    public async Task ReserveTicketsForOrderAsync(
        IEnumerable<EventTicketReservation> reservations,
        CancellationToken cancellationToken = default)
    {
        var groupedReservations = reservations
            .GroupBy(reservation => reservation.EventId)
            .Select(group => new EventTicketReservation(
                group.Key,
                group.Aggregate(0, (total, reservation) => checked(total + reservation.Quantity))))
            .OrderBy(reservation => reservation.EventId)
            .ToList();

        if (groupedReservations.Count == 0)
        {
            throw new ArgumentException("At least one event reservation is required.", nameof(reservations));
        }

        if (groupedReservations.Any(reservation => reservation.EventId <= 0 || reservation.Quantity <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(reservations), "Event IDs and quantities must be positive.");
        }

        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Ticket reservations must run inside the order transaction.");
        }

        foreach (var reservation in groupedReservations)
        {
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

    public async Task<int> CancelOrderRegistrationsAsync(
        int orderId,
        DateTime? canceledAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        var canceledAt = canceledAtUtc ?? DateTime.UtcNow;
        canceledAt = canceledAt.Kind switch
        {
            DateTimeKind.Utc => canceledAt,
            DateTimeKind.Local => canceledAt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(canceledAt, DateTimeKind.Utc)
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
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

        await transaction.CommitAsync(cancellationToken);
        return totalCanceled;
    }
}
