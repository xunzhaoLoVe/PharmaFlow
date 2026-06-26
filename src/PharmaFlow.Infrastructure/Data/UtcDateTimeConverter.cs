using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PharmaFlow.Infrastructure.Data;

/// <summary>
/// 将 DateTime 统一转为 UTC，兼容 PostgreSQL timestamp with time zone
/// </summary>
public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    { }
}
