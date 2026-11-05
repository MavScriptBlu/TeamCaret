using System.ComponentModel.DataAnnotations;

namespace CCOS.Shared;

/// <summary>
/// Request body for PUT /api/events/{id} (CAR-55, CAR-56). Same fields and rules as
/// <see cref="CreateEventRequest"/>, plus IsActive: false removes the event and true restores it.
/// Events are never deleted, so IsActive is the only way an event goes away.
/// </summary>
/// <param name="Name">The event name.</param>
/// <param name="Description">Optional text about the event.</param>
/// <param name="StartsAtUtc">When the event starts, in UTC.</param>
/// <param name="EndsAtUtc">When the event ends, in UTC. Must be after the start.</param>
/// <param name="VenueId">Which venue holds the event.</param>
/// <param name="TicketCapacity">The most tickets that can be sold. Can't drop below what's already sold.</param>
/// <param name="MembersOnly">True when only club members may buy tickets.</param>
/// <param name="TicketPrice">Regular price of the event's ticket.</param>
/// <param name="MemberTicketPrice">Member price of the event's ticket. Can't be more than the regular price.</param>
/// <param name="IsActive">True keeps the event available, false removes it.</param>
public record UpdateEventRequest(
    [Required(ErrorMessage = "Event name is required.")]
    [StringLength(100, ErrorMessage = "Event name can be at most 100 characters.")]
    string Name,

    [StringLength(500, ErrorMessage = "Description can be at most 500 characters.")]
    string? Description,

    DateTime StartsAtUtc,

    DateTime EndsAtUtc,

    [Range(1, int.MaxValue, ErrorMessage = "Pick a venue.")]
    int VenueId,

    [Range(1, int.MaxValue, ErrorMessage = "Ticket capacity must be at least 1.")]
    int TicketCapacity,

    bool MembersOnly,

    [Range(0, 99999999.99, ErrorMessage = "Ticket price must be at least 0.")]
    decimal TicketPrice,

    [Range(0, 99999999.99, ErrorMessage = "Member price must be at least 0.")]
    decimal MemberTicketPrice,

    bool IsActive = true) : IValidatableObject
{
    /// <summary>
    /// Cross-field rules shared with the create request.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        EventRules.Check(StartsAtUtc, EndsAtUtc, TicketPrice, MemberTicketPrice);
}
