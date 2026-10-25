namespace CCOS.Repositories;

/// <summary>
/// Thrown when an event can't hand out the tickets that were asked for, either because it is
/// sold out, it doesn't have enough seats left, or it has been deactivated.
/// Callers catch this to show "sold out" instead of a crash.
/// </summary>
public sealed class SoldOutException(int eventId)
    : InvalidOperationException($"Event {eventId} is sold out or has insufficient capacity.")
{
    /// <summary>The event that ran out of room.</summary>
    public int EventId { get; } = eventId;
}
