using CCOS.Data;
using CCOS.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CCOS.Repositories;

/// <summary>
/// Read operations for events. The "Upcoming events" query from the schema doc:
/// IsActive and StartsAtUtc from now on, soonest first.
/// </summary>
public sealed class EventRepository(AppDbContext db)
{
    /// <summary>
    /// Upcoming events: active, starting from now on, soonest first.
    /// Includes the venue name and active ticket types for the event list.
    /// </summary>
    public async Task<IReadOnlyList<Event>> ListUpcomingAsync(CancellationToken cancellationToken = default)
    {
        return await db.Events
            .AsNoTracking()
            .Include(@event => @event.Venue)
            .Include(@event => @event.TicketTypes.Where(t => t.IsActive))
            .Where(@event => @event.IsActive && @event.StartsAtUtc >= DateTime.UtcNow)
            .OrderBy(@event => @event.StartsAtUtc)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// One active event with its venue and active ticket types, or null if not found.
    /// </summary>
    public async Task<Event?> FindActiveAsync(int eventId, CancellationToken cancellationToken = default)
    {
        return await db.Events
            .AsNoTracking()
            .Include(@event => @event.Venue)
            .Include(@event => @event.TicketTypes.Where(t => t.IsActive))
            .Where(@event => @event.IsActive)
            .FirstOrDefaultAsync(@event => @event.EventId == eventId, cancellationToken);
    }
}
