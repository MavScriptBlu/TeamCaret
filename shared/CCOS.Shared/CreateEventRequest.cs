using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace CCOS.Shared;

/// <summary>
/// request body for POST /api/events. Creates a new event and its first ticket type
/// </summary>
/// <param name="VenueId"></param>
/// <param name="Name"></param>
/// <param name="Description"></param>
/// <param name="StartsAtUtc"></param>
/// <param name="EndsAtUtc"></param>
/// <param name="TicketCapacity"></param>
/// <param name="MembersOnly"></param>
/// <param name="Price"></param>
/// <param name="MemberPrice"></param>
public record CreateEventRequest(
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

    [Range(0, double.MaxValue, ErrorMessage = "Price must be at least 0.")]
    decimal Price,

    [Range(0, double.MaxValue, ErrorMessage = "Member price must be at least 0.")]
    decimal MemberPrice) : IValidatableObject
{
    private static readonly string[] EndsAtUtcFieldNames = [nameof(EndsAtUtc)];
    private static readonly string[] MemberPriceFieldNames = [nameof(MemberPrice)];

    /// <summary>
    /// check that EndsAtUtc is after StartsAtUtc,
    /// and that MemberPrice does not exceed Price
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndsAtUtc <= StartsAtUtc)
        {
            yield return new ValidationResult(
                "End time must be after start time.",
                EndsAtUtcFieldNames);
        }
        if (MemberPrice > Price)
        {
            yield return new ValidationResult(
                "Member price cannot exceed regular price.",
                MemberPriceFieldNames);
        }
    }

}