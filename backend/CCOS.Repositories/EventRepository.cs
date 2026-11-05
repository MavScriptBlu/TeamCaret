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

    /// <summary>
    /// Creates a new event with one ticket type,
    /// or returns validation errors if the venue or schedule is invalid.
    /// </summary>
    /// <param name="venueId"></param>
    /// <param name="name"></param>
    /// <param name="description"></param>
    /// <param name="startsAtUtc"></param>
    /// <param name="endsAtUtc"></param>
    /// <param name="ticketCapacity"></param>
    /// <param name="membersOnly"></param>
    /// <param name="price"></param>
    /// <param name="memberPrice"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<EventWriteResult> CreateAsync(
        int venueId,
        string name,
        string? description,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        int ticketCapacity,
        bool membersOnly,
        decimal price,
        decimal memberPrice,
        CancellationToken cancellationToken = default
        )
    {
        var startsAt = startsAtUtc.UtcDateTime;
        var endsAt = endsAtUtc.UtcDateTime;

        var validation = await ValidateVenueAndScheduleAsync(
            venueId,
            startsAt,
            endsAt,
            ticketCapacity,
            ticketsSold: null,
            eventIdToExclude: null,
            cancellationToken);

        if (validation.Errors.Count > 0 || validation.Venue is null)
        {
            return EventWriteResult.Invalid(validation.Errors);
        }

        var @event = new Event
        {
            VenueId = venueId,
            Venue = validation.Venue,
            Name = name,
            Description = description,
            StartsAtUtc = startsAt,
            EndsAtUtc = endsAt,
            TicketCapacity = ticketCapacity,
            MembersOnly = membersOnly,
            IsActive = true,
            TicketTypes =
            [
                new TicketType
                {
                    Category = "Tickets",
                    Name = "General",
                    Price = price,
                    MemberPrice = memberPrice,
                    StockQuantity = 0,
                    IsActive = true
                }
            ]
        };

        db.Events.Add(@event);

        // saves the event and the ticket type, and sets their IDs
        await db.SaveChangesAsync(cancellationToken);

        return EventWriteResult.Success(@event);
    }

    /// <summary>
    /// Updates only the fields allowed by the event update contract
    /// returns null if te event does not exist or field keyed errors if validation fails
    /// </summary>
    public async Task<EventWriteResult?> UpdateAsync(
        int eventId,
        int venueId,
        string name,
        string? description,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        int ticketCapacity,
        bool membersOnly,
        uint rowVersion,
        CancellationToken cancellationToken = default)
    {
        var @event = await db.Events
           .Include(existingEvent => existingEvent.Venue)
           .Include(existingEvent => existingEvent.TicketTypes
               .Where(ticketType => ticketType.IsActive))
           .FirstOrDefaultAsync(existingEvent => existingEvent.EventId == eventId, cancellationToken);

        if (@event is null)
        {
            return null;
        }

        var startsAt = startsAtUtc.UtcDateTime;
        var endsAt = endsAtUtc.UtcDateTime;


        var validation = await ValidateVenueAndScheduleAsync(
            venueId,
            startsAt,
            endsAt,
            ticketCapacity,
            @event.TicketsSold,
            eventId,
            cancellationToken);

        if (validation.Errors.Count > 0 || validation.Venue is null)
        {
            return EventWriteResult.Invalid(validation.Errors);
        }

        @event.VenueId = venueId;
        @event.Venue = validation.Venue;
        @event.Name = name;
        @event.Description = description;
        @event.StartsAtUtc = startsAt;
        @event.EndsAtUtc = endsAt;
        @event.TicketCapacity = ticketCapacity;
        @event.MembersOnly = membersOnly;

        // use the clients last seen RowVersion to detect if the event was changed by someone else since the client loaded it
        // if the RowVersion in the database is different, EF will throw a DbUpdateConcurrencyException
        db.Entry(@event).Property(existingEvent => existingEvent.RowVersion).OriginalValue = rowVersion;

        // Ensure even an otherwise unchanged PUT checks the client's token against the database.
        // RowVersion is database-generated, so mark an ordinary field modified instead.
        db.Entry(@event).Property(existingEvent => existingEvent.Name).IsModified = true;

        await db.SaveChangesAsync(cancellationToken);

        return EventWriteResult.Success(@event);
    }

    /// <summary>
    /// Soft-deactivates an active event. Returns false if it is missing or already inactive.
    /// </summary>
    public async Task<bool> DeactivateAsync(int eventId, CancellationToken cancellationToken = default)
    {
        var @event = await db.Events.FirstOrDefaultAsync( existingEvent => existingEvent.EventId == eventId && existingEvent.IsActive, cancellationToken);

        if (@event is null)
        {
            return false;
        }

        @event.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// validates that the venue exists and is active, that the ticket capacity is within the venue's capacity
    /// </summary>
    private async Task<(Venue? Venue, Dictionary<string, string[]> Errors)> ValidateVenueAndScheduleAsync(
        int venueId,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        int ticketCapacity,
        int? ticketsSold,
        int? eventIdToExclude,
        CancellationToken cancellationToken)
    {
        var venue = await db.Venues.FirstOrDefaultAsync(
            candidate => candidate.VenueId == venueId && candidate.IsActive,
            cancellationToken);

        if (venue is null)
        {
            // return a validation error for the venueId field if the venue does not exist or is inactive
            return (null,new Dictionary<string, string[]>{[nameof(Event.VenueId)] = ["Venue does not exist or is inactive."]});
        }

        var errors = new Dictionary<string, string[]>();

        if (ticketCapacity > venue.Capacity)
        {
            // return a validation error for the ticketCapacity field - if the ticket capacity exceeds the venue's capacity
            errors[nameof(Event.TicketCapacity)] =
                [$"Ticket capacity cannot exceed the venue's capacity of {venue.Capacity}."];
        }

        if (ticketsSold is int sold && ticketCapacity < sold)
        {
            // return a validation error for the ticketCapacity field - if the ticket capacity is less than the number of tickets already sold
            errors[nameof(Event.TicketCapacity)] =
                [$"Ticket capacity cannot be lower than the {sold} tickets already sold."];
        }

        // checks for overlapping events at the same venue.
        // An event overlaps if its start time is before the end time of another event,
        // and its end time is after the start time of that other event.
        var overlappingEvents = db.Events.Where(existingEvent =>
            existingEvent.IsActive
            && existingEvent.VenueId == venueId
            && startsAtUtc < existingEvent.EndsAtUtc
            && existingEvent.StartsAtUtc < endsAtUtc);

        if (eventIdToExclude is int currentEventId)
        {
            overlappingEvents = overlappingEvents.Where(existingEvent => existingEvent.EventId != currentEventId);
        }

        if (await overlappingEvents.AnyAsync(cancellationToken))
        {
            errors[nameof(Event.StartsAtUtc)] =
                ["This venue already has an active event during that time."];
        }

        return (venue, errors);
    }

}

/// <summary>
/// Result of an attempt to write an event.
/// Either the event (success) or a dictionary of errors (invalid).
/// </summary>
/// <param name="Event"></param>
/// <param name="Errors"></param>
public sealed record EventWriteResult(Event? Event, IReadOnlyDictionary<string, string[]> Errors)
{
    public static EventWriteResult Success(Event @event) => new(@event, new Dictionary<string, string[]>());

    public static EventWriteResult Invalid(IReadOnlyDictionary<string, string[]> errors) => new(null, errors);
}
