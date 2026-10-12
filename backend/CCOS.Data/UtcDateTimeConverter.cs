using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CCOS.Data;

public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value,
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
