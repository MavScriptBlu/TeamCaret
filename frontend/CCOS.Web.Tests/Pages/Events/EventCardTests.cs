using CCOS.Web.Pages.Events;

namespace CCOS.Web.Tests.Pages.Events;

/// <summary>
/// What an event card on /Events shows (CAR-76): its prices, which follow the same rule as the
/// event page, and its time line.
/// </summary>
public class EventCardTests
{
    // An event on Friday Nov 20, 2026, 5:00–7:00 PM club time (CST is UTC-6).
    private static EventInfo Event(bool membersOnly, params TicketTypeInfo[] ticketTypes) => new(
        1,
        "Capture the Flag Night",
        "Team up and solve security puzzles for prizes.",
        new DateTime(2026, 11, 20, 23, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 11, 21, 1, 0, 0, DateTimeKind.Utc),
        50,
        membersOnly,
        true,
        3,
        "Cougar Commons",
        ticketTypes,
        []);

    // ---- Prices ----

    [Fact]
    public void ForEvent_Member_ShowsBothPrices()
    {
        var prices = TicketPrices.ForEvent(Event(false, new TicketTypeInfo("General", 10m, 5m)));

        Assert.Equal("$10.00", prices.Regular);
        Assert.Equal("$5.00", prices.Member);
    }

    [Fact]
    public void ForEvent_NotAMember_ShowsOnlyTheRegularPrice()
    {
        // The API leaves the member price out for visitors who aren't current members (CAR-69).
        var prices = TicketPrices.ForEvent(Event(false, new TicketTypeInfo("General", 10m, null)));

        Assert.Equal("$10.00", prices.Regular);
        Assert.Null(prices.Member);
    }

    [Fact]
    public void ForEvent_SamePriceForEveryone_ShowsItOnce()
    {
        var prices = TicketPrices.ForEvent(Event(false, new TicketTypeInfo("General", 5m, 5m)));

        Assert.Equal("$5.00", prices.Regular);
        Assert.Null(prices.Member);
    }

    [Fact]
    public void ForEvent_MembersOnly_ShowsJustTheMemberPrice()
    {
        var prices = TicketPrices.ForEvent(Event(true, new TicketTypeInfo("General", 0m, 0m)));

        Assert.Null(prices.Regular);
        Assert.Equal("Free", prices.Member);
    }

    [Fact]
    public void ForEvent_MembersOnlyAndNotAMember_ShowsNoPrice()
    {
        var eventInfo = Event(true, new TicketTypeInfo("General", 0m, null));

        Assert.True(TicketPrices.ForEvent(eventInfo).IsEmpty);
        Assert.True(TicketPrices.MemberPriceHidden(eventInfo));
    }

    [Fact]
    public void ForEvent_NoTicketsOnSale_ShowsNoPrice()
    {
        var eventInfo = Event(false, new TicketTypeInfo("General", 10m, 5m, IsActive: false));

        Assert.True(TicketPrices.ForEvent(eventInfo).IsEmpty);
        Assert.False(TicketPrices.MemberPriceHidden(eventInfo));
    }

    // ---- Time line ----

    [Fact]
    public void FormatTimeRange_SameDay_ShowsBothTimes()
    {
        var text = ClubTime.FormatTimeRange(
            new DateTime(2026, 11, 20, 23, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 21, 1, 0, 0, DateTimeKind.Utc));

        Assert.Equal("5:00 PM – 7:00 PM", text);
    }

    [Fact]
    public void FormatTimeRange_EndsTheNextDay_AddsTheEndDate()
    {
        // 9:00 PM Friday to 1:00 AM Saturday, club time.
        var text = ClubTime.FormatTimeRange(
            new DateTime(2026, 11, 21, 3, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 21, 7, 0, 0, DateTimeKind.Utc));

        Assert.Equal("9:00 PM – Nov 21, 1:00 AM", text);
    }
}
