using System.Runtime.CompilerServices;
using System.Text;
using CCOS.Data;
using CCOS.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CCOS.Repositories;

/// <summary>
/// Handles the ticket counts on events: holding seats when an order is placed and giving
/// them back when an order is canceled.
///
/// Every change to Events.TicketsSold goes through a conditional UPDATE in the database, so two
/// people buying the last ticket at the same time can't both get it (CAR-30). An order that
/// touches several events still uses one UPDATE for all of them, not one per event, so a big
/// order doesn't make a trip to the database for every ticket line.
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

        // One UPDATE for the whole order. (This used to be one UPDATE per event, which is the
        // "N+1" pattern: the more ticket lines, the more trips to the database.)
        //
        // The UPDATE only changes anything when EVERY event in the order is active and has room,
        // so if one event is full, none of them are touched. The room check and the increase
        // happen together in the database, so there is no gap for another buyer. The room check
        // is also repeated on the row being changed: that copy is the one the database re-checks
        // if another buyer sneaks in first. CK_Events_TicketsSold is the last safety net.
        //
        // Tradeoff: the old loop took events one at a time in event id order so two orders could
        // never lock each other out. One statement locks its rows in whatever order the database
        // picks, so the database's own deadlock detection protects us now. If it ever chooses an
        // order as the loser, that order fails and the caller can retry it.
        var arguments = ToArguments(groupedReservations);
        var eventIds = IdList(groupedReservations.Count);

        var reserved = await ExecuteRawAsync(
            $"""
            UPDATE "Events"
            SET "TicketsSold" = "TicketsSold" + {AmountCase("\"EventId\"", groupedReservations.Count)}
            WHERE "EventId" IN ({eventIds})
              AND "IsActive"
              AND CAST("TicketsSold" AS BIGINT) + {AmountCase("\"EventId\"", groupedReservations.Count)} <= "TicketCapacity"
              AND NOT EXISTS (
                SELECT 1 FROM "Events" AS "other"
                WHERE "other"."EventId" IN ({eventIds})
                  AND NOT ("other"."IsActive"
                    AND CAST("other"."TicketsSold" AS BIGINT) + {AmountCase("\"other\".\"EventId\"", groupedReservations.Count)} <= "other"."TicketCapacity"))
            """,
            arguments,
            cancellationToken);

        // Fewer rows than events means an event was full, inactive or missing. The caller rolls
        // back the order transaction when it sees SoldOutException.
        if (reserved != groupedReservations.Count)
        {
            throw new SoldOutException(await FindFirstUnavailableEventAsync(groupedReservations, cancellationToken));
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

        // Read the order's live tickets once, so we know how many seats go back to each event.
        var purchased = await db.TicketRegistrations
            .Where(registration =>
                registration.OrderLine.OrderId == orderId
                && registration.Status == RegistrationStatus.Purchased)
            .Select(registration => registration.EventId)
            .ToListAsync(cancellationToken);

        // Nothing left to cancel (never bought, or already canceled): done, and safe to run again.
        if (purchased.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return 0;
        }

        // Step 1: mark every purchased ticket on the order as canceled, in one UPDATE (this used to
        // be one per event). Only "Purchased" rows match, so running this twice never cancels
        // anything twice.
        var canceled = await db.TicketRegistrations
            .Where(registration =>
                registration.OrderLine.OrderId == orderId
                && registration.Status == RegistrationStatus.Purchased)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(registration => registration.Status, RegistrationStatus.Canceled)
                    .SetProperty(registration => registration.CanceledAtUtc, canceledAt),
                cancellationToken);

        // Someone else canceled the same order at the same moment and got there first.
        // Their cancel already handled everything, so there is nothing for us to do.
        if (canceled == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return 0;
        }

        // Only some of the tickets were still purchased, so we can't tell how many seats each event
        // gets back. Stop (the transaction is rolled back) and let the caller try again.
        if (canceled != purchased.Count)
        {
            throw new InvalidOperationException(
                $"Order {orderId} changed while it was being canceled. Nothing was changed; try again.");
        }

        // Step 2: give the seats back, again in one UPDATE for all the events. The
        // "TicketsSold >= amount" check keeps the count from dropping below zero if the numbers
        // were ever out of sync.
        var seatsBack = purchased
            .GroupBy(eventId => eventId)
            .OrderBy(group => group.Key)
            .Select(group => new EventTicketReservation(group.Key, group.Count()))
            .ToList();

        var decremented = await ExecuteRawAsync(
            $"""
            UPDATE "Events"
            SET "TicketsSold" = "TicketsSold" - {AmountCase("\"EventId\"", seatsBack.Count)}
            WHERE "EventId" IN ({IdList(seatsBack.Count)})
              AND "TicketsSold" >= {AmountCase("\"EventId\"", seatsBack.Count)}
            """,
            ToArguments(seatsBack),
            cancellationToken);

        if (decremented != seatsBack.Count)
        {
            throw new InvalidOperationException(
                "An event has fewer sold tickets than the registrations being canceled "
                + $"(events {string.Join(", ", seatsBack.Select(seats => seats.EventId))}).");
        }

        // Only save the cancellations once every event has been updated.
        await transaction.CommitAsync(cancellationToken);
        return canceled;
    }

    /// <summary>
    /// Runs one raw UPDATE with its numbers sent as real parameters (never pasted into the text),
    /// so nothing a visitor types can reach the SQL. The text only ever holds the {0}, {1}... slots.
    /// </summary>
    /// <returns>How many rows the UPDATE changed.</returns>
    private Task<int> ExecuteRawAsync(string sql, object[] arguments, CancellationToken cancellationToken)
    {
        return db.Database.ExecuteSqlAsync(FormattableStringFactory.Create(sql, arguments), cancellationToken);
    }

    /// <summary>
    /// The values for the slots: event id then amount, for each event. Slot 0 and 1 are the first
    /// event, slot 2 and 3 the second, and so on.
    /// </summary>
    private static object[] ToArguments(IReadOnlyList<EventTicketReservation> reservations)
    {
        var arguments = new object[reservations.Count * 2];
        for (var index = 0; index < reservations.Count; index++)
        {
            arguments[index * 2] = reservations[index].EventId;
            arguments[(index * 2) + 1] = reservations[index].Quantity;
        }

        return arguments;
    }

    /// <summary>
    /// The slots for every event id, like "{0}, {2}, {4}".
    /// </summary>
    private static string IdList(int eventCount) =>
        string.Join(", ", Enumerable.Range(0, eventCount).Select(index => $"{{{index * 2}}}"));

    /// <summary>
    /// A CASE that gives each event its own amount, like
    /// CASE "EventId" WHEN {0} THEN {1} WHEN {2} THEN {3} ELSE 0 END.
    /// </summary>
    private static string AmountCase(string idColumn, int eventCount)
    {
        var text = new StringBuilder("CASE ").Append(idColumn);
        for (var index = 0; index < eventCount; index++)
        {
            text.Append($" WHEN {{{index * 2}}} THEN {{{(index * 2) + 1}}}");
        }

        return text.Append(" ELSE 0 END").ToString();
    }

    /// <summary>
    /// After a failed reserve, finds the first event (lowest id) that can't give the seats asked
    /// for, so the exception can name it. Nothing was changed, so this sees the state from before.
    /// </summary>
    private async Task<int> FindFirstUnavailableEventAsync(
        IReadOnlyList<EventTicketReservation> reservations,
        CancellationToken cancellationToken)
    {
        var eventIds = reservations.Select(reservation => reservation.EventId).ToList();

        var states = await db.Events
            .AsNoTracking()
            .Where(@event => eventIds.Contains(@event.EventId))
            .Select(@event => new { @event.EventId, @event.IsActive, @event.TicketsSold, @event.TicketCapacity })
            .ToListAsync(cancellationToken);

        foreach (var reservation in reservations)
        {
            var state = states.FirstOrDefault(candidate => candidate.EventId == reservation.EventId);

            if (state is null || !state.IsActive || state.TicketsSold + (long)reservation.Quantity > state.TicketCapacity)
            {
                return reservation.EventId;
            }
        }

        // Every event looks fine now, so another buyer must have just left. Name the first one.
        return reservations[0].EventId;
    }
}
