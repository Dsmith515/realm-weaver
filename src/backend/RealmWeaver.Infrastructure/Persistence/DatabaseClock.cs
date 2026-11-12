namespace RealmWeaver.Infrastructure.Persistence;

public static class DatabaseClock
{
    /// <summary>
    /// The current UTC time truncated to whole microseconds, the precision of a Postgres <c>timestamptz</c>.
    /// Without this, a value returned from a create differs from the same value read back later.
    /// </summary>
    public static DateTimeOffset GetDatabaseUtcNow(this TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var now = clock.GetUtcNow();
        return now.AddTicks(-(now.Ticks % (TimeSpan.TicksPerMillisecond / 1000)));
    }
}
