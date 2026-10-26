using CCOS.Data;
using CCOS.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CCOS.Repositories;

/// <summary>
/// Reads and creates venues (where events are held). Each venue has at most one street address.
/// Venues are never deleted; IsActive = false retires one (schema doc).
/// </summary>
public sealed class VenueRepository(AppDbContext db)
{
    /// <summary>
    /// Active venues, by name, for the event form's venue dropdown.
    /// </summary>
    public async Task<IReadOnlyList<Venue>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        return await db.Venues.AsNoTracking()
            .Where(venue => venue.IsActive)
            .OrderBy(venue => venue.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// True when a venue (active or retired) already uses this name, ignoring capitalization.
    /// Names are unique in the database (UX_Venues_Name).
    /// </summary>
    public Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        var lowered = name.Trim().ToLower();
        return db.Venues.AnyAsync(venue => venue.Name.ToLower() == lowered, cancellationToken);
    }

    /// <summary>
    /// CREATEs a venue and its street address in one save, so there's never a venue without one.
    /// </summary>
    public async Task<Venue> CreateAsync(
        string name,
        string addressLine1,
        string? addressLine2,
        string city,
        string state,
        string zipCode,
        int capacity,
        CancellationToken cancellationToken = default)
    {
        var venue = new Venue
        {
            Name = name.Trim(),
            Capacity = capacity,
            IsActive = true,
            Address = new VenueAddress
            {
                AddressLine1 = addressLine1.Trim(),
                AddressLine2 = string.IsNullOrWhiteSpace(addressLine2) ? null : addressLine2.Trim(),
                City = city.Trim(),
                State = state.Trim().ToUpperInvariant(),
                ZipCode = zipCode.Trim(),
            },
        };

        db.Venues.Add(venue);
        await db.SaveChangesAsync(cancellationToken);
        return venue;
    }
}
