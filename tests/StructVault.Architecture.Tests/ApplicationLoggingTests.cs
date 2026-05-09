using StructVault.Application.Abstractions.Logging;
using StructVault.Application.Logging;
using StructVault.Infrastructure.Logging;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class ApplicationLoggingTests
{
    [Fact]
    public async Task HandlerWritesValidatedLogEntryThroughWriter()
    {
        RecordingApplicationLogWriter writer = new();
        WriteApplicationLogCommandHandler handler = new(writer);
        DateTimeOffset timestamp = new(2026, 5, 9, 10, 30, 0, TimeSpan.Zero);

        await handler.Handle(
            new WriteApplicationLogCommand(
                ApplicationLogLevel.Information,
                " Desktop ",
                " InitialVaultLoaded ",
                " startup complete ",
                timestamp),
            CancellationToken.None);

        Assert.NotNull(writer.Entry);
        Assert.Equal(timestamp, writer.Entry.OccurredAtUtc);
        Assert.Equal(ApplicationLogLevel.Information, writer.Entry.Level);
        Assert.Equal("Desktop", writer.Entry.Category);
        Assert.Equal("InitialVaultLoaded", writer.Entry.EventName);
        Assert.Equal("startup complete", writer.Entry.Detail);
    }

    [Fact]
    public void CommandRejectsBlankCategory()
    {
        Assert.Throws<ArgumentException>(() =>
            new WriteApplicationLogCommand(ApplicationLogLevel.Warning, " ", "EventName"));
    }

    [Fact]
    public void CommandRejectsInvalidLogLevel()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WriteApplicationLogCommand((ApplicationLogLevel)999, "Desktop", "EventName"));
    }

    [Fact]
    public async Task HandlerHonorsCancellationBeforeWriting()
    {
        RecordingApplicationLogWriter writer = new();
        WriteApplicationLogCommandHandler handler = new(writer);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await handler.Handle(
                new WriteApplicationLogCommand(ApplicationLogLevel.Information, "Desktop", "InitialVaultLoaded"),
                cancellation.Token));

        Assert.Null(writer.Entry);
    }

    [Fact]
    public async Task FileSystemWriterCreatesDirectoryAndAppendsEntries()
    {
        string directoryPath = CreateUniqueTempDirectoryPath();
        string logFilePath = Path.Combine(directoryPath, "logs", "structvault.log");
        FileSystemApplicationLogWriter writer = new(logFilePath);

        try
        {
            await writer.WriteAsync(
                new ApplicationLogEntry(
                    new DateTimeOffset(2026, 5, 9, 10, 30, 0, TimeSpan.Zero),
                    ApplicationLogLevel.Information,
                    "Desktop",
                    "InitialVaultLoaded",
                    null),
                CancellationToken.None);
            await writer.WriteAsync(
                new ApplicationLogEntry(
                    new DateTimeOffset(2026, 5, 9, 10, 31, 0, TimeSpan.Zero),
                    ApplicationLogLevel.Error,
                    "Desktop",
                    "InitialVaultLoadFailed",
                    "System.InvalidOperationException"),
                CancellationToken.None);

            string[] lines = await File.ReadAllLinesAsync(logFilePath);

            Assert.Equal(2, lines.Length);
            Assert.Contains("Information | Desktop | InitialVaultLoaded", lines[0], StringComparison.Ordinal);
            Assert.Contains("Error | Desktop | InitialVaultLoadFailed | System.InvalidOperationException", lines[1], StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Fact]
    public async Task FileSystemWriterNormalizesNewLinesToKeepOneRecordPerLine()
    {
        string directoryPath = CreateUniqueTempDirectoryPath();
        string logFilePath = Path.Combine(directoryPath, "structvault.log");
        FileSystemApplicationLogWriter writer = new(logFilePath);

        try
        {
            await writer.WriteAsync(
                new ApplicationLogEntry(
                    DateTimeOffset.UtcNow,
                    ApplicationLogLevel.Warning,
                    "Desktop\nCategory",
                    "Operational\rEvent",
                    "multi\nline\rdetail"),
                CancellationToken.None);

            string[] lines = await File.ReadAllLinesAsync(logFilePath);

            Assert.Single(lines);
            Assert.DoesNotContain('\n', lines[0]);
            Assert.DoesNotContain('\r', lines[0]);
            Assert.Contains("Desktop Category", lines[0], StringComparison.Ordinal);
            Assert.Contains("Operational Event", lines[0], StringComparison.Ordinal);
            Assert.Contains("multi line detail", lines[0], StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    private static string CreateUniqueTempDirectoryPath()
    {
        return Path.Combine(Path.GetTempPath(), "StructVaultTests", Guid.NewGuid().ToString("N"));
    }

    private static void DeleteDirectoryIfExists(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    private sealed class RecordingApplicationLogWriter : IApplicationLogWriter
    {
        public ApplicationLogEntry? Entry { get; private set; }

        public Task WriteAsync(ApplicationLogEntry entry, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Entry = entry;

            return Task.CompletedTask;
        }
    }
}
