using MediatR;
using StructVault.Application.Abstractions.IdleLock;

namespace StructVault.Application.IdleLock;

public sealed class LockVaultAfterIdleTimeoutCommandHandler : IRequestHandler<LockVaultAfterIdleTimeoutCommand, VaultLockState>
{
    private readonly IIdleActivityTracker idleActivityTracker;

    public LockVaultAfterIdleTimeoutCommandHandler(IIdleActivityTracker idleActivityTracker)
    {
        this.idleActivityTracker = idleActivityTracker ?? throw new ArgumentNullException(nameof(idleActivityTracker));
    }

    public Task<VaultLockState> Handle(LockVaultAfterIdleTimeoutCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        IdleActivitySnapshot snapshot = idleActivityTracker.GetSnapshot(request.IdleTimeout, request.ObservedAtUtc);
        VaultLockState state = new(snapshot.IsIdle, snapshot.LastActivityUtc, snapshot.ObservedAtUtc, snapshot.IdleDuration);
        return Task.FromResult(state);
    }
}
