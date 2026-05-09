using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.IdleLock;

public sealed class RecordUserActivityCommand : ICommand<DateTimeOffset>
{
    public RecordUserActivityCommand(DateTimeOffset? activityUtc = null)
    {
        ActivityUtc = activityUtc?.ToUniversalTime();
    }

    public DateTimeOffset? ActivityUtc { get; }
}
