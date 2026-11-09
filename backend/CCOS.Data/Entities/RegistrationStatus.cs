namespace CCOS.Data.Entities;

/// <summary>
/// Whether a ticket is still good. Saved in the database as text ("Purchased" or "Canceled").
/// </summary>
public enum RegistrationStatus
{
    /// <summary>The ticket was bought and counts toward the event's TicketsSold.</summary>
    Purchased,

    /// <summary>The ticket was canceled and its seat was given back.</summary>
    Canceled
}
