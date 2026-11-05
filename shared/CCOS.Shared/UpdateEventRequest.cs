using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace CCOS.Shared;


/// <summary>
/// Request body for PUT /api/events/{id}. Updates an existing event.
/// Validation: venue, name, and ticket capacity required
/// </summary>
public record UpdateEventRequest(
    [Range(1, int.MaxValue, ErrorMessage = "Venue is required.")]
    int VenueId,

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    string Name,

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    string? Description,

    DateTimeOffset StartsAtUtc,

    DateTimeOffset EndsAtUtc,

    [Range(1, int.MaxValue, ErrorMessage = "Ticket capacity must be greater than 0.")]
    int TicketCapacity,

    bool MembersOnly,

    // concurency token: lets the server detect a stale update
    // and return a 409 Conflict instead of overwriting the other user's changes.
    uint RowVersion) : IValidatableObject
{
    private static readonly string[] EndsAtUtcFieldNames = [nameof(EndsAtUtc)];

    /// <summary>
    /// Cross-field validation: EndsAtUtc must be after StartsAtUtc.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndsAtUtc <= StartsAtUtc)
        {
            yield return new ValidationResult(
                "End time must be after start time.",
                EndsAtUtcFieldNames);
        }
    }
}