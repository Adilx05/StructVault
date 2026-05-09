using System.Globalization;
using System.Text;
using StructVault.Application.Abstractions.Logging;

namespace StructVault.Infrastructure.Logging;

public sealed class FileSystemApplicationLogWriter : IApplicationLogWriter
{
    private const int MaxFieldLength = 512;
    private readonly string logFilePath;
    private readonly SemaphoreSlim writeLock = new(1, 1);

    public FileSystemApplicationLogWriter(string logFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logFilePath);
        this.logFilePath = Path.GetFullPath(logFilePath);
    }

    public async Task WriteAsync(ApplicationLogEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        string? directoryPath = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        string line = FormatEntry(entry);
        await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using FileStream stream = new(
                logFilePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 8192,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            await using StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private static string FormatEntry(ApplicationLogEntry entry)
    {
        StringBuilder builder = new();
        builder.Append(entry.OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture));
        builder.Append(" | ");
        builder.Append(entry.Level);
        builder.Append(" | ");
        builder.Append(Truncate(entry.Category));
        builder.Append(" | ");
        builder.Append(Truncate(entry.EventName));

        if (!string.IsNullOrWhiteSpace(entry.Detail))
        {
            builder.Append(" | ");
            builder.Append(Truncate(entry.Detail));
        }

        return builder.ToString();
    }

    private static string Truncate(string value)
    {
        return value.Length <= MaxFieldLength ? value : value[..MaxFieldLength];
    }
}
