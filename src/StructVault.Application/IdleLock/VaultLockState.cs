namespace StructVault.Application.IdleLock;

public sealed class VaultLockState
{
    public VaultLockState(bool isLocked, DateTimeOffset lastActivityUtc, DateTimeOffset observedAtUtc, TimeSpan idleDuration)
    {
        LastActivityUtc = lastActivityUtc.ToUniversalTime();
        ObservedAtUtc = observedAtUtc.ToUniversalTime();
        IdleDuration = idleDuration < TimeSpan.Zero ? TimeSpan.Zero : idleDuration;
        IsLocked = isLocked;
    }

    public bool IsLocked { get; }

    public DateTimeOffset LastActivityUtc { get; }

    public DateTimeOffset ObservedAtUtc { get; }

    public TimeSpan IdleDuration { get; }
}
