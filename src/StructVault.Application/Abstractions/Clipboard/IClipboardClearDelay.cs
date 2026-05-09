namespace StructVault.Application.Abstractions.Clipboard;

public interface IClipboardClearDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
