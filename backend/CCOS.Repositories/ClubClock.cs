namespace CCOS.Repositories;

/// <summary>
/// Supplies the club's "today": the date in America/Chicago, not the server's UTC date (CAR-28).
/// Every membership check uses it, so dues don't lapse at 7 p.m. the evening before
/// and every screen agrees on what day it is.
/// </summary>
public sealed class ClubClock
{
    /// <summary>The club's time zone. Windows and Linux both understand this IANA name in .NET 6 and later.</summary>
    public const string ClubTimeZoneId = "America/Chicago";

    /// <summary>Today's date in the club's time zone.</summary>
    public DateOnly Today => ToClubDate(DateTime.UtcNow);

    /// <summary>
    /// Turns a UTC moment into the club's local date. Separate from Today so tests can pass any time.
    /// </summary>
    public static DateOnly ToClubDate(DateTime utcNow) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
            DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
            ClubTimeZoneId));
}
