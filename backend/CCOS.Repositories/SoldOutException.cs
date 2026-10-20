namespace CCOS.Repositories;

/// <summary>
/// Exception thrown when an event is sold out or has insufficient capacity.
/// </summary>
public sealed class SoldOutException(int eventId)
    : InvalidOperationException($"Event {eventId} is sold out or has insufficient capacity.")
{
    public int EventId { get; } = eventId;
}
