namespace CCOS.Repositories;

public sealed class SoldOutException(int eventId)
    : InvalidOperationException($"Event {eventId} is sold out or has insufficient capacity.")
{
    public int EventId { get; } = eventId;
}
