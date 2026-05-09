using StructVault.Application.Abstractions.Clipboard;

namespace StructVault.Application.Clipboard;

public sealed class ClipboardAutoClearService : IClipboardAutoClearService, IDisposable
{
    private readonly IClipboardService clipboardService;
    private readonly IClipboardClearDelay clearDelay;
    private readonly object syncRoot = new();
    private CancellationTokenSource? pendingClearCancellation;
    private bool disposed;

    public ClipboardAutoClearService(IClipboardService clipboardService, IClipboardClearDelay clearDelay)
    {
        this.clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        this.clearDelay = clearDelay ?? throw new ArgumentNullException(nameof(clearDelay));
    }

    public Task? PendingClearTask { get; private set; }

    public Task ScheduleClearAsync(string copiedText, TimeSpan delay, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (copiedText is null)
        {
            throw new ArgumentNullException(nameof(copiedText));
        }

        if (delay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Clipboard clear delay must be greater than zero.");
        }

        CancellationTokenSource clearCancellation = new();
        Task clearTask;
        CancellationTokenSource? previousCancellation;
        lock (syncRoot)
        {
            previousCancellation = pendingClearCancellation;
            pendingClearCancellation = clearCancellation;
            clearTask = ClearAfterDelayAsync(copiedText, delay, clearCancellation);
            PendingClearTask = clearTask;
        }

        previousCancellation?.Cancel();
        return Task.CompletedTask;
    }

    public Task CancelPendingClearAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        CancellationTokenSource? cancellation;
        lock (syncRoot)
        {
            cancellation = pendingClearCancellation;
            pendingClearCancellation = null;
            PendingClearTask = null;
        }

        cancellation?.Cancel();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        CancellationTokenSource? cancellation;
        lock (syncRoot)
        {
            cancellation = pendingClearCancellation;
            pendingClearCancellation = null;
            PendingClearTask = null;
        }

        cancellation?.Cancel();
        cancellation?.Dispose();
    }

    private async Task ClearAfterDelayAsync(string copiedText, TimeSpan delay, CancellationTokenSource clearCancellation)
    {
        try
        {
            await clearDelay.DelayAsync(delay, clearCancellation.Token).ConfigureAwait(false);
            string? currentText = await clipboardService.GetTextAsync(clearCancellation.Token).ConfigureAwait(false);
            if (string.Equals(currentText, copiedText, StringComparison.Ordinal))
            {
                await clipboardService.ClearAsync(clearCancellation.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (clearCancellation.IsCancellationRequested)
        {
        }
        catch (Exception) when (!clearCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            lock (syncRoot)
            {
                if (ReferenceEquals(pendingClearCancellation, clearCancellation))
                {
                    pendingClearCancellation = null;
                    PendingClearTask = null;
                }
            }

            clearCancellation.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(ClipboardAutoClearService));
        }
    }
}
