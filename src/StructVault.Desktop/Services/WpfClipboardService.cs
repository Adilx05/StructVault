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
}
