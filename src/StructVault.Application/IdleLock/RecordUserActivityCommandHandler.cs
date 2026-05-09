using MediatR;
using StructVault.Application.Abstractions.IdleLock;

namespace StructVault.Application.IdleLock;

public sealed class RecordUserActivityCommandHandler : IRequestHandler<RecordUserActivityCommand, DateTimeOffset>
{
    private readonly IIdleActivityTracker idleActivityTracker;

    public RecordUserActivityCommandHandler(IIdleActivityTracker idleActivityTracker)
    {
        this.idleActivityTracker = idleActivityTracker ?? throw new ArgumentNullException(nameof(idleActivityTracker));
    }

    public Task<DateTimeOffset> Handle(RecordUserActivityCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset lastActivityUtc = idleActivityTracker.RecordActivity(request.ActivityUtc);
        return Task.FromResult(lastActivityUtc);
    }
}
