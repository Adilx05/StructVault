using StructVault.Application.Abstractions.Logging;
using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Logging;

public sealed class WriteApplicationLogCommand : ICommand
{
    public WriteApplicationLogCommand(
        ApplicationLogLevel level,
        string category,
        string eventName,
        string? detail = null,
        DateTimeOffset? occurredAtUtc = null)
    {
        Entry = new ApplicationLogEntry(
            occurredAtUtc ?? DateTimeOffset.UtcNow,
            level,
            category,
            eventName,
            detail);
    }

    public ApplicationLogEntry Entry { get; }
}
