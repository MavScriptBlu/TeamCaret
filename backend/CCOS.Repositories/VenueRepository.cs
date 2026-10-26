using CCOS.Data;
using CCOS.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CCOS.Repositories;

/// <summary>
/// Read operations for venues. Active venues only (retired venues can't host new events).
/// </summary>
public sealed class VenueRepository(AppDbContext db)
{
    /// <summary>
    /// Active venues sorted by name, for the event form's venue dropdown.
    /// </summary>
    public async Task<IReadOnlyList<Venue>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        return await db.Venues
            .AsNoTracking()
            .Where(venue => venue.IsActive)
            .OrderBy(venue => venue.Name)
            .ToListAsync(cancellationToken);
    }
}
