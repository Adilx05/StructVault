using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.IdleLock;

public sealed class LockVaultAfterIdleTimeoutCommand : ICommand<VaultLockState>
{
    public LockVaultAfterIdleTimeoutCommand(TimeSpan idleTimeout, DateTimeOffset? observedAtUtc = null)
    {
        if (idleTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(idleTimeout), idleTimeout, "Idle timeout must be greater than zero.");
        }

        IdleTimeout = idleTimeout;
        ObservedAtUtc = observedAtUtc?.ToUniversalTime();
    }

    public TimeSpan IdleTimeout { get; }

    public DateTimeOffset? ObservedAtUtc { get; }
}
