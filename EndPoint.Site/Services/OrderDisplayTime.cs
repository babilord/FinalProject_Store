namespace EndPoint.Site.Services;

/// <summary>Converts UTC order timestamps for display without changing stored values.</summary>
public sealed class OrderDisplayTime
{
    private readonly TimeZoneInfo displayTimeZone;

    public OrderDisplayTime(IConfiguration configuration)
    {
        var timeZoneId = configuration["OrderDisplay:TimeZoneId"]
            ?? throw new InvalidOperationException("OrderDisplay:TimeZoneId must be configured.");
        displayTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    public DateTime FromUtc(DateTime value)
    {
        // SQL datetime values may be materialized with an Unspecified Kind.
        return TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(value, DateTimeKind.Utc), displayTimeZone);
    }
}
