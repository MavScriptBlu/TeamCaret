using System.Globalization;
using CCOS.Shared;
using CCOS.Web.Pages.Events;

namespace CCOS.Web.Services;

/// <summary>
/// The warning in the "Remove venue?" dialog when upcoming events are booked at the venue (CAR-79).
/// The team decided to warn, not block: removing a venue only takes it off the venue list and the
/// event form's dropdown, and the events keep their venue and still happen.
/// </summary>
public static class VenueRemoveWarning
{
    /// <summary>How many event names the warning lists before it says "and N more".</summary>
    public const int MaxNamesShown = 3;

    /// <summary>
    /// The warning text, or null when no upcoming events are booked there (no warning shown).
    /// For example: "2 upcoming events are booked here: Capture the Flag Night (Nov 17) and
    /// Intro to Linux Workshop (Nov 24). They keep this venue and still happen."
    /// </summary>
    /// <param name="upcomingEvents">The venue's upcoming events, soonest first (from the API).</param>
    public static string? For(IReadOnlyList<VenueEventSummaryDto>? upcomingEvents)
    {
        if (upcomingEvents is null || upcomingEvents.Count == 0)
        {
            return null;
        }

        var names = upcomingEvents
            .Take(MaxNamesShown)
            .Select(@event => $"{@event.Name} ({ClubTime.ToClubTime(@event.StartsAtUtc).ToString("MMM d", CultureInfo.InvariantCulture)})")
            .ToList();

        var more = upcomingEvents.Count - names.Count;
        var list = more > 0
            ? $"{string.Join(", ", names)} and {more} more"
            : JoinWithAnd(names);

        return upcomingEvents.Count == 1
            ? $"1 upcoming event is booked here: {list}. It keeps this venue and still happens."
            : $"{upcomingEvents.Count} upcoming events are booked here: {list}. They keep this venue and still happen.";
    }

    // "A", "A and B", "A, B and C".
    private static string JoinWithAnd(IReadOnlyList<string> items) =>
        items.Count <= 1
            ? string.Concat(items)
            : $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}";
}
