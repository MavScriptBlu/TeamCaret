using CCOS.Data;
using CCOS.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CCOS.Repositories;

/// <summary>
/// Reads, creates, updates and removes venues (where events are held). Each venue has at most one
/// street address. Venues are never deleted; IsActive = false removes one and IsActive = true
/// restores it (schema doc).
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
    /// Every venue, removed ones too, with its address, by name. This is the officer list
    /// (CAR-57) so a removed venue can be found and restored.
    /// </summary>
    public async Task<IReadOnlyList<Venue>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.Venues.AsNoTracking()
            .Include(venue => venue.Address)
            .OrderBy(venue => venue.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// One venue with its address, or null when there's no venue with that id. Removed venues
    /// are found too, so an officer can open one to restore it.
    /// </summary>
    public async Task<Venue?> FindAsync(int venueId, CancellationToken cancellationToken = default)
    {
        return await db.Venues.AsNoTracking()
            .Include(venue => venue.Address)
            .FirstOrDefaultAsync(venue => venue.VenueId == venueId, cancellationToken);
    }

    /// <summary>
    /// True when a venue (active or removed) already uses this name, ignoring capitalization.
    /// Names are unique in the database (UX_Venues_Name).
    /// </summary>
    /// <param name="name">The name to look for.</param>
    /// <param name="exceptVenueId">
    /// A venue to skip, so editing a venue doesn't clash with its own name.
    /// </param>
    /// <param name="cancellationToken">Lets the caller stop the work early.</param>
    public Task<bool> NameExistsAsync(
        string name,
        int? exceptVenueId = null,
        CancellationToken cancellationToken = default)
    {
        var lowered = name.Trim().ToLower();
        return db.Venues.AnyAsync(
            venue => venue.Name.ToLower() == lowered && venue.VenueId != exceptVenueId,
            cancellationToken);
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

    /// <summary>
    /// UPDATEs a venue and its address in one save. A venue that was saved without an address
    /// gets one. IsActive false removes the venue, true restores it.
    /// </summary>
    /// <returns>The updated venue, or null when there's no venue with that id.</returns>
    public async Task<Venue?> UpdateAsync(
        int venueId,
        string name,
        string addressLine1,
        string? addressLine2,
        string city,
        string state,
        string zipCode,
        int capacity,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var venue = await db.Venues
            .Include(candidate => candidate.Address)
            .FirstOrDefaultAsync(candidate => candidate.VenueId == venueId, cancellationToken);

        if (venue is null)
        {
            return null;
        }

        venue.Name = name.Trim();
        venue.Capacity = capacity;
        venue.IsActive = isActive;

        // The sample venues have no address yet, so make one the first time an officer edits them.
        venue.Address ??= new VenueAddress();
        venue.Address.AddressLine1 = addressLine1.Trim();
        venue.Address.AddressLine2 = string.IsNullOrWhiteSpace(addressLine2) ? null : addressLine2.Trim();
        venue.Address.City = city.Trim();
        venue.Address.State = state.Trim().ToUpperInvariant();
        venue.Address.ZipCode = zipCode.Trim();

        await db.SaveChangesAsync(cancellationToken);
        return venue;
    }

    /// <summary>
    /// REMOVEs a venue: sets IsActive to false. The row stays in the database (soft delete),
    /// and events that already use the venue keep showing its name.
    /// Removing a venue that's already removed still counts as success.
    /// </summary>
    /// <returns>False when there's no venue with that id.</returns>
    public async Task<bool> SoftDeleteAsync(int venueId, CancellationToken cancellationToken = default)
    {
        var venue = await db.Venues.FirstOrDefaultAsync(candidate => candidate.VenueId == venueId, cancellationToken);

        if (venue is null)
        {
            return false;
        }

        if (venue.IsActive)
        {
            venue.IsActive = false;
            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}
