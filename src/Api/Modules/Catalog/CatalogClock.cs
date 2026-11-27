namespace Gearnix.Api.Modules.Catalog;

/// <summary>"Now" for catalog timestamps.</summary>
public static class CatalogClock
{
    /// <summary>
    /// The current UTC time, cut to whole microseconds: PostgreSQL stores no finer, so the value returned right after
    /// saving equals the value read back later.
    /// </summary>
    public static DateTimeOffset UtcNow(TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
