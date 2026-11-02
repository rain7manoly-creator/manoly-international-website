namespace ManolyWarehouse.Extensions;

/// <summary>
/// Timestamps are stored in UTC. The warehouse is in Palestine, so everything
/// shown to staff is converted to local time (Asia/Hebron, which follows DST).
/// </summary>
public static class LocalTimeExtensions
{
    private static readonly TimeZoneInfo Zone = ResolveZone();

    private static TimeZoneInfo ResolveZone()
    {
        foreach (var id in new[] { "Asia/Hebron", "Asia/Jerusalem", "Israel Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        // Container without tz data — fixed UTC+2 is right for most of the year.
        return TimeZoneInfo.CreateCustomTimeZone("Local+2", TimeSpan.FromHours(2), "UTC+2", "UTC+2");
    }

    public static DateTime ToLocal(this DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    /// <summary>e.g. "2026/11/02 15:21"</summary>
    public static string ToLocalString(this DateTime utc) => utc.ToLocal().ToString("yyyy/MM/dd HH:mm");
}
