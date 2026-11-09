namespace CCOS.Repositories;

/// <summary>
/// One request to hold tickets: how many tickets to take from which event.
/// An order with several ticket lines becomes a list of these.
/// </summary>
/// <param name="EventId">The event the tickets are for.</param>
/// <param name="Quantity">How many tickets to hold. Must be more than zero.</param>
public sealed record EventTicketReservation(int EventId, int Quantity);
