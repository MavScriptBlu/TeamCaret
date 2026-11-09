using System.ComponentModel.DataAnnotations;

namespace CCOS.Shared;

/// <summary>
/// The rules that look at more than one event field at a time, shared by the create and update
/// requests so both say the same thing.
/// </summary>
public static class EventRules
{
    // Built once instead of on every validation run.
    private static readonly string[] EndFieldNames = [nameof(CreateEventRequest.EndsAtUtc)];
    private static readonly string[] MemberPriceFieldNames = [nameof(CreateEventRequest.MemberTicketPrice)];

    /// <summary>
    /// The event has to end after it starts, and members can't pay more than everyone else.
    /// </summary>
    public static IEnumerable<ValidationResult> Check(
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        decimal ticketPrice,
        decimal memberTicketPrice)
    {
        if (endsAtUtc <= startsAtUtc)
        {
            yield return new ValidationResult("The event has to end after it starts.", EndFieldNames);
        }

        if (memberTicketPrice > ticketPrice)
        {
            yield return new ValidationResult("Member price cannot exceed regular price.", MemberPriceFieldNames);
        }
    }
}
