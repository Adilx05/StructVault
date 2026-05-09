namespace StructVault.Application.Abstractions.IdleLock;

public sealed class IdleActivitySnapshot
{
    public IdleActivitySnapshot(DateTimeOffset lastActivityUtc, DateTimeOffset observedAtUtc, TimeSpan idleDuration, bool isIdle)
    {
        LastActivityUtc = lastActivityUtc.ToUniversalTime();
        ObservedAtUtc = observedAtUtc.ToUniversalTime();
        IdleDuration = idleDuration < TimeSpan.Zero ? TimeSpan.Zero : idleDuration;
        IsIdle = isIdle;
    }

    public DateTimeOffset LastActivityUtc { get; }

    public DateTimeOffset ObservedAtUtc { get; }

    public TimeSpan IdleDuration { get; }

    public bool IsIdle { get; }
}
