using System.ComponentModel.DataAnnotations;

namespace CCOS.Shared;

/// <summary>
/// Request body for POST /api/venues. Creates a venue and its street address in one save
/// (CAR-51). Lengths match the Venues and VenueAddresses columns; names must be unique,
/// which the API checks against the database.
/// </summary>
/// <param name="Name">The venue name, like "Main Campus, Room 101".</param>
/// <param name="AddressLine1">Street address.</param>
/// <param name="AddressLine2">Building or room (optional).</param>
/// <param name="City">City.</param>
/// <param name="State">Two-letter state code, like WI.</param>
/// <param name="ZipCode">ZIP code.</param>
/// <param name="Capacity">How many people the room holds.</param>
public record CreateVenueRequest(
    [Required(ErrorMessage = "Venue name is required.")]
    [StringLength(100, ErrorMessage = "Venue name can be at most 100 characters.")]
    string Name,

    [Required(ErrorMessage = "Street address is required.")]
    [StringLength(100, ErrorMessage = "Street address can be at most 100 characters.")]
    string AddressLine1,

    [StringLength(100, ErrorMessage = "Building or room can be at most 100 characters.")]
    string? AddressLine2,

    [Required(ErrorMessage = "City is required.")]
    [StringLength(50, ErrorMessage = "City can be at most 50 characters.")]
    string City,

    [Required(ErrorMessage = "State is required.")]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "Use the two-letter state code, like WI.")]
    string State,

    [Required(ErrorMessage = "ZIP code is required.")]
    [StringLength(10, ErrorMessage = "ZIP code can be at most 10 characters.")]
    string ZipCode,

    [Range(1, int.MaxValue, ErrorMessage = "Capacity must be at least 1.")]
    int Capacity);
