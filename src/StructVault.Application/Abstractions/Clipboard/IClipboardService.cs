namespace StructVault.Application.Abstractions.Clipboard;

public interface IClipboardService
{
    Task SetTextAsync(string text, CancellationToken cancellationToken);

    Task<string?> GetTextAsync(CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}
