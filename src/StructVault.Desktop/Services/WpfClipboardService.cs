using System.Windows;
using StructVault.Application.Abstractions.Clipboard;

namespace StructVault.Desktop.Services;

internal sealed class WpfClipboardService : IClipboardService
{
    public Task SetTextAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Clipboard.SetText(text ?? string.Empty);
        return Task.CompletedTask;
    }

    public Task<string?> GetTextAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(Clipboard.ContainsText() ? Clipboard.GetText() : null);
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Clipboard.Clear();
        return Task.CompletedTask;
    }
}
