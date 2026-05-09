using StructVault.Application.Abstractions.Clipboard;

namespace StructVault.Application.Clipboard;

public sealed class SystemClipboardClearDelay : IClipboardClearDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (delay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Clipboard clear delay must be greater than zero.");
        }

        return Task.Delay(delay, cancellationToken);
    }
}
