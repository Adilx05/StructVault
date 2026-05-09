using MediatR;
using StructVault.Application.Abstractions.IdleLock;

namespace StructVault.Application.IdleLock;

public sealed class GetIdleActivityStateQueryHandler : IRequestHandler<GetIdleActivityStateQuery, IdleActivityState>
{
    private readonly IIdleActivityTracker idleActivityTracker;

    public GetIdleActivityStateQueryHandler(IIdleActivityTracker idleActivityTracker)
    {
        this.idleActivityTracker = idleActivityTracker ?? throw new ArgumentNullException(nameof(idleActivityTracker));
    }

    public Task<IdleActivityState> Handle(GetIdleActivityStateQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        IdleActivitySnapshot snapshot = idleActivityTracker.GetSnapshot(request.IdleTimeout, request.ObservedAtUtc);
        IdleActivityState state = new(snapshot.LastActivityUtc, snapshot.ObservedAtUtc, snapshot.IdleDuration, snapshot.IsIdle);
        return Task.FromResult(state);
    }
}
