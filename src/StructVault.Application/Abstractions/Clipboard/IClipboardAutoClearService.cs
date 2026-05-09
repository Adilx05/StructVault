namespace StructVault.Application.Abstractions.Clipboard;

public interface IClipboardAutoClearService
{
    Task ScheduleClearAsync(string copiedText, TimeSpan delay, CancellationToken cancellationToken);

    Task CancelPendingClearAsync(CancellationToken cancellationToken);
}
