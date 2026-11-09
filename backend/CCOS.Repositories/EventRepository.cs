using CCOS.Data;
using CCOS.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CCOS.Repositories;

/// <summary>
/// Reads, creates, updates and removes events. The public list is the "Upcoming events" query from
/// the schema doc: IsActive and StartsAtUtc from now on, soonest first. Events are never deleted;
/// IsActive = false removes one and IsActive = true restores it (CAR-55, CAR-56).
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
    /// One upcoming event (active, starting from now on) with its venue and active ticket types,
    /// or null. Same rule as ListUpcomingAsync, so a past event can't be opened by its link (CAR-69).
    /// </summary>
    public async Task<Event?> FindUpcomingAsync(int eventId, CancellationToken cancellationToken = default)
    {
        return await db.Events
            .AsNoTracking()
            .Include(@event => @event.Venue)
            .Include(@event => @event.TicketTypes.Where(t => t.IsActive))
            .Where(@event => @event.IsActive && @event.StartsAtUtc >= DateTime.UtcNow)
            .FirstOrDefaultAsync(@event => @event.EventId == eventId, cancellationToken);
    }

    /// <summary>
    /// Every event, removed and past ones too, newest start first. This is the officer list
    /// (CAR-56) so a removed event can be found and restored.
    /// </summary>
    public async Task<IReadOnlyList<Event>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.Events
            .AsNoTracking()
            .Include(@event => @event.Venue)
            .Include(@event => @event.TicketTypes.Where(t => t.IsActive))
            .OrderByDescending(@event => @event.StartsAtUtc)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// One event in any state (active or removed) with its venue and active ticket types,
    /// or null if not found. Officers use this to open a removed event and restore it.
    /// </summary>
    public async Task<Event?> FindAsync(int eventId, CancellationToken cancellationToken = default)
    {
        return await db.Events
            .AsNoTracking()
            .Include(@event => @event.Venue)
            .Include(@event => @event.TicketTypes.Where(t => t.IsActive))
            .FirstOrDefaultAsync(@event => @event.EventId == eventId, cancellationToken);
    }

    /// <summary>
    /// True when the venue exists and, if asked, is still active. A removed venue can't be
    /// picked for a new event.
    /// </summary>
    public Task<bool> VenueAvailableAsync(
        int venueId,
        bool mustBeActive,
        CancellationToken cancellationToken = default)
    {
        return db.Venues.AnyAsync(
            venue => venue.VenueId == venueId && (!mustBeActive || venue.IsActive),
            cancellationToken);
    }

    /// <summary>
    /// CREATEs an event and its ticket type in one save, so an event never goes live without a price.
    /// </summary>
    public async Task<Event> CreateAsync(
        string name,
        string? description,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        int venueId,
        int ticketCapacity,
        bool membersOnly,
        decimal ticketPrice,
        decimal memberTicketPrice,
        CancellationToken cancellationToken = default)
    {
        var @event = new Event
        {
            Name = name.Trim(),
            Description = CleanDescription(description),
            StartsAtUtc = ToUtc(startsAtUtc),
            EndsAtUtc = ToUtc(endsAtUtc),
            VenueId = venueId,
            TicketCapacity = ticketCapacity,
            MembersOnly = membersOnly,
            IsActive = true,
        };

        @event.TicketTypes.Add(NewTicketType(ticketPrice, memberTicketPrice));

        db.Events.Add(@event);
        await db.SaveChangesAsync(cancellationToken);
        return @event;
    }

    /// <summary>
    /// UPDATEs an event and the prices on its ticket type in one save. IsActive false removes the
    /// event, true restores it. The capacity can't drop below the tickets already sold.
    /// If someone else saved the same event first, EF throws DbUpdateConcurrencyException and
    /// the caller turns that into a 409.
    /// </summary>
    public async Task<EventUpdateResult> UpdateAsync(
        int eventId,
        string name,
        string? description,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        int venueId,
        int ticketCapacity,
        bool membersOnly,
        decimal ticketPrice,
        decimal memberTicketPrice,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var @event = await db.Events
            .Include(candidate => candidate.TicketTypes.Where(t => t.IsActive))
            .FirstOrDefaultAsync(candidate => candidate.EventId == eventId, cancellationToken);

        if (@event is null)
        {
            return new EventUpdateResult(EventUpdateStatus.NotFound, null, 0);
        }

        if (ticketCapacity < @event.TicketsSold)
        {
            return new EventUpdateResult(EventUpdateStatus.CapacityBelowSold, null, @event.TicketsSold);
        }

        @event.Name = name.Trim();
        @event.Description = CleanDescription(description);
        @event.StartsAtUtc = ToUtc(startsAtUtc);
        @event.EndsAtUtc = ToUtc(endsAtUtc);
        @event.VenueId = venueId;
        @event.TicketCapacity = ticketCapacity;
        @event.MembersOnly = membersOnly;
        @event.IsActive = isActive;

        // The form has one price box, so it changes the first ticket type still on sale.
        // An event with none (all pulled by hand) gets a fresh "General" one.
        var ticketType = @event.TicketTypes.FirstOrDefault();
        if (ticketType is null)
        {
            @event.TicketTypes.Add(NewTicketType(ticketPrice, memberTicketPrice));
        }
        else
        {
            ticketType.Price = ticketPrice;
            ticketType.MemberPrice = memberTicketPrice;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new EventUpdateResult(EventUpdateStatus.Saved, @event, @event.TicketsSold);
    }

    /// <summary>
    /// REMOVEs an event: sets IsActive to false. The row stays in the database (soft delete),
    /// along with its ticket types and registrations. Removing an event that's already removed
    /// still counts as success.
    /// </summary>
    /// <returns>False when there's no event with that id.</returns>
    public async Task<bool> SoftDeleteAsync(int eventId, CancellationToken cancellationToken = default)
    {
        var @event = await db.Events.FirstOrDefaultAsync(candidate => candidate.EventId == eventId, cancellationToken);

        if (@event is null)
        {
            return false;
        }

        if (@event.IsActive)
        {
            @event.IsActive = false;
            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    /// <summary>A new "General" ticket type, the one every event starts with.</summary>
    private static TicketType NewTicketType(decimal price, decimal memberPrice) => new()
    {
        Category = "Tickets",
        Name = "General",
        Price = price,
        MemberPrice = memberPrice,
        StockQuantity = 0,
    };

    private static string? CleanDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    /// <summary>
    /// Makes sure a time is marked as UTC before it is saved. A time with an offset is converted;
    /// one with no kind is taken as already UTC.
    /// </summary>
    private static DateTime ToUtc(DateTime time) => time.Kind switch
    {
        DateTimeKind.Local => time.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(time, DateTimeKind.Utc),
        _ => time,
    };
}

/// <summary>What happened when an event update was tried.</summary>
public enum EventUpdateStatus
{
    /// <summary>The event was saved.</summary>
    Saved,

    /// <summary>There's no event with that id.</summary>
    NotFound,

    /// <summary>The new capacity is lower than the tickets already sold.</summary>
    CapacityBelowSold,
}

/// <summary>The outcome of <see cref="EventRepository.UpdateAsync"/>.</summary>
/// <param name="Status">What happened.</param>
/// <param name="Event">The saved event when Status is Saved, otherwise null.</param>
/// <param name="TicketsSold">Tickets already sold, so the caller can say why a capacity was refused.</param>
public sealed record EventUpdateResult(EventUpdateStatus Status, Event? Event, int TicketsSold);
