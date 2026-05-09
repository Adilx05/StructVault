namespace StructVault.Application.Abstractions.IdleLock;

public interface IIdleActivityTracker
{
    DateTimeOffset LastActivityUtc { get; }

    DateTimeOffset RecordActivity(DateTimeOffset? activityUtc = null);

    IdleActivitySnapshot GetSnapshot(TimeSpan idleTimeout, DateTimeOffset? observedAtUtc = null);
}
