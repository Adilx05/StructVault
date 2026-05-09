namespace StructVault.Application.Abstractions.Clipboard;

public interface IClipboardService
{
    Task SetTextAsync(string text, CancellationToken cancellationToken);
}
