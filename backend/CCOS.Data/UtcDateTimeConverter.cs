using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CCOS.Data;

/// <summary>
/// Makes sure times read from the database come back marked as UTC.
/// Databases hand back times with no time zone attached, which makes DateTime.Kind "Unspecified"
/// and can cause wrong-hour bugs later. Writing a value changes nothing; reading one stamps it as UTC.
/// </summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value,
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
