using System;
using System.Collections.Generic;
using CCOS.Shared;
using CCOS.Web.Services;
using Xunit;

namespace CCOS.Web.Tests.Services;

/// <summary>The warning in the "Remove venue?" dialog when events are booked there (CAR-79).</summary>
public class VenueRemoveWarningTests
{
    // 6 PM club time (CST, UTC-6) on the given November 2026 day.
    private static VenueEventSummaryDto Event(int id, string name, int novemberDay) =>
        new(id, name, new DateTime(2026, 11, novemberDay, 0, 0, 0, DateTimeKind.Utc).AddHours(24));

    [Fact]
    public void For_NoUpcomingEvents_ReturnsNull()
    {
        Assert.Null(VenueRemoveWarning.For(null));
        Assert.Null(VenueRemoveWarning.For([]));
    }

    [Fact]
    public void For_OneEvent_NamesIt()
    {
        var warning = VenueRemoveWarning.For([Event(1, "Capture the Flag Night", 17)]);

        Assert.Equal(
            "1 upcoming event is booked here: Capture the Flag Night (Nov 17). It keeps this venue and still happens.",
            warning);
    }

    [Fact]
    public void For_ThreeEvents_NamesAllOfThem()
    {
        var warning = VenueRemoveWarning.For(
        [
            Event(1, "Capture the Flag Night", 17),
            Event(2, "Intro to Linux Workshop", 18),
            Event(3, "Resume Night", 20),
        ]);

        Assert.Equal(
            "3 upcoming events are booked here: Capture the Flag Night (Nov 17), Intro to Linux Workshop (Nov 18) and Resume Night (Nov 20). They keep this venue and still happen.",
            warning);
    }

    [Fact]
    public void For_MoreThanThreeEvents_SaysHowManyMore()
    {
        var events = new List<VenueEventSummaryDto>
        {
            Event(1, "A", 17),
            Event(2, "B", 18),
            Event(3, "C", 19),
            Event(4, "D", 20),
            Event(5, "E", 21),
        };

        var warning = VenueRemoveWarning.For(events);

        Assert.Equal(
            "5 upcoming events are booked here: A (Nov 17), B (Nov 18), C (Nov 19) and 2 more. They keep this venue and still happen.",
            warning);
    }
}
