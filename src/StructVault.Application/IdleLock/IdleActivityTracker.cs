using StructVault.Application.Abstractions.IdleLock;

namespace StructVault.Application.IdleLock;

public sealed class IdleActivityTracker : IIdleActivityTracker
{
    private readonly TimeProvider timeProvider;
    private readonly object syncRoot = new();
    private DateTimeOffset lastActivityUtc;

    public IdleActivityTracker(TimeProvider? timeProvider = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
        lastActivityUtc = NormalizeUtc(this.timeProvider.GetUtcNow(), nameof(timeProvider));
    }

    public DateTimeOffset LastActivityUtc
    {
        get
        {
            lock (syncRoot)
            {
                return lastActivityUtc;
            }
        }
    }

    public DateTimeOffset RecordActivity(DateTimeOffset? activityUtc = null)
    {
        DateTimeOffset observedAtUtc = NormalizeUtc(timeProvider.GetUtcNow(), nameof(activityUtc));
        DateTimeOffset normalizedActivityUtc = NormalizeUtc(activityUtc ?? observedAtUtc, nameof(activityUtc));
        if (normalizedActivityUtc > observedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(activityUtc), activityUtc, "Activity time cannot be in the future.");
        }

        lock (syncRoot)
        {
            lastActivityUtc = normalizedActivityUtc;
            return lastActivityUtc;
        }
    }

    public IdleActivitySnapshot GetSnapshot(TimeSpan idleTimeout, DateTimeOffset? observedAtUtc = null)
    {
        if (idleTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(idleTimeout), idleTimeout, "Idle timeout must be greater than zero.");
        }

        DateTimeOffset currentUtc = NormalizeUtc(observedAtUtc ?? timeProvider.GetUtcNow(), nameof(observedAtUtc));
        DateTimeOffset activityUtc;
        lock (syncRoot)
        {
            activityUtc = lastActivityUtc;
        }

        TimeSpan idleDuration = currentUtc - activityUtc;
        if (idleDuration < TimeSpan.Zero)
        {
            idleDuration = TimeSpan.Zero;
        }

        return new IdleActivitySnapshot(activityUtc, currentUtc, idleDuration, idleDuration >= idleTimeout);
    }

    private static DateTimeOffset NormalizeUtc(DateTimeOffset value, string parameterName)
    {
        if (value == DateTimeOffset.MinValue || value == DateTimeOffset.MaxValue)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Timestamp must be a valid finite date and time.");
        }

        return value.ToUniversalTime();
    }
}
