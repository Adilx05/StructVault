using System.Data.Common;
using System.Text;
using MediatR;
using Microsoft.Data.Sqlite;
using StructVault.Application.Abstractions.Clipboard;
using StructVault.Application.Abstractions.Persistence;
using StructVault.Application.Clipboard;
using StructVault.Application.Persistence;
using StructVault.Desktop.Commands;
using StructVault.Desktop.ViewModels;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultClipboardSecurityTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 5, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CopyVaultFieldValueToClipboardCommandHandlerCopiesUtf8FieldValue()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();
        VaultFieldRecord field = CreateField("field-password", "node-root", "Password", "correct horse battery staple");
        RecordingFieldReader fieldReader = new(field);
        RecordingClipboardService clipboardService = new();
        CopyVaultFieldValueToClipboardCommandHandler handler = new(fieldReader, clipboardService, new RecordingClipboardAutoClearService());

        await handler.Handle(new CopyVaultFieldValueToClipboardCommand(connection, " field-password "), CancellationToken.None);

        Assert.Same(connection, fieldReader.LastConnection);
        Assert.Equal("field-password", fieldReader.LastQuery?.Id);
        Assert.Equal("correct horse battery staple", clipboardService.Text);
    }

    [Fact]
    public async Task CopyVaultFieldValueToClipboardCommandHandlerRejectsMissingField()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();
        CopyVaultFieldValueToClipboardCommandHandler handler = new(new RecordingFieldReader(null), new RecordingClipboardService(), new RecordingClipboardAutoClearService());

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.Handle(new CopyVaultFieldValueToClipboardCommand(connection, "missing-field"), CancellationToken.None));

        Assert.Contains("could not be found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CopyVaultFieldValueToClipboardCommandHandlerRejectsBinaryFieldValue()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();
        VaultFieldRecord field = new("field-binary", "node-root", "Attachment", new byte[] { 0xFF, 0xFE, 0xFD }, 0, Timestamp, Timestamp);
        RecordingClipboardService clipboardService = new();
        CopyVaultFieldValueToClipboardCommandHandler handler = new(new RecordingFieldReader(field), clipboardService, new RecordingClipboardAutoClearService());

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.Handle(new CopyVaultFieldValueToClipboardCommand(connection, "field-binary"), CancellationToken.None));

        Assert.Contains("UTF-8 text", exception.Message, StringComparison.Ordinal);
        Assert.Null(clipboardService.Text);
    }


    [Fact]
    public async Task CopyVaultFieldValueToClipboardCommandHandlerSchedulesAutoClearWithConfiguredDelay()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();
        VaultFieldRecord field = CreateField("field-token", "node-root", "Token", "temporary-secret");
        RecordingClipboardService clipboardService = new();
        RecordingClipboardAutoClearService autoClearService = new();
        CopyVaultFieldValueToClipboardCommandHandler handler = new(new RecordingFieldReader(field), clipboardService, autoClearService);
        TimeSpan configuredDelay = TimeSpan.FromSeconds(12);

        await handler.Handle(
            new CopyVaultFieldValueToClipboardCommand(connection, "field-token", autoClearEnabled: true, autoClearDelay: configuredDelay),
            CancellationToken.None);

        Assert.Equal("temporary-secret", clipboardService.Text);
        Assert.Equal("temporary-secret", autoClearService.ScheduledText);
        Assert.Equal(configuredDelay, autoClearService.ScheduledDelay);
        Assert.False(autoClearService.CancelPendingClearCalled);
    }

    [Fact]
    public async Task ClipboardAutoClearServiceClearsCopiedValueAfterTimeout()
    {
        RecordingClipboardService clipboardService = new();
        ControllableClipboardClearDelay delay = new();
        ClipboardAutoClearService autoClearService = new(clipboardService, delay);
        await clipboardService.SetTextAsync("temporary-secret", CancellationToken.None);

        await autoClearService.ScheduleClearAsync("temporary-secret", TimeSpan.FromSeconds(5), CancellationToken.None);
        await delay.WaitForDelayRequestAsync();

        Task? pendingClearTask = autoClearService.PendingClearTask;
        Assert.NotNull(pendingClearTask);
        delay.CompleteDelay();
        await pendingClearTask!;

        Assert.Null(clipboardService.Text);
    }

    [Fact]
    public async Task ClipboardAutoClearServiceDoesNotClearClipboardChangedByAnotherProcess()
    {
        RecordingClipboardService clipboardService = new();
        ControllableClipboardClearDelay delay = new();
        ClipboardAutoClearService autoClearService = new(clipboardService, delay);
        await clipboardService.SetTextAsync("temporary-secret", CancellationToken.None);

        await autoClearService.ScheduleClearAsync("temporary-secret", TimeSpan.FromSeconds(5), CancellationToken.None);
        await delay.WaitForDelayRequestAsync();
        await clipboardService.SetTextAsync("other-application-value", CancellationToken.None);

        Task? pendingClearTask = autoClearService.PendingClearTask;
        Assert.NotNull(pendingClearTask);
        delay.CompleteDelay();
        await pendingClearTask!;

        Assert.Equal("other-application-value", clipboardService.Text);
    }

    [Fact]
    public async Task CopyVaultFieldValueToClipboardCommandHandlerDisablesAutoClearWhenRequested()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();
        VaultFieldRecord field = CreateField("field-note", "node-root", "Note", "keep-on-clipboard");
        RecordingClipboardAutoClearService autoClearService = new();
        CopyVaultFieldValueToClipboardCommandHandler handler = new(
            new RecordingFieldReader(field),
            new RecordingClipboardService(),
            autoClearService);

        await handler.Handle(
            new CopyVaultFieldValueToClipboardCommand(connection, "field-note", autoClearEnabled: false),
            CancellationToken.None);

        Assert.True(autoClearService.CancelPendingClearCalled);
        Assert.Null(autoClearService.ScheduledText);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CopyVaultFieldValueToClipboardCommandRejectsInvalidAutoClearDelay(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CopyVaultFieldValueToClipboardCommand(new SqliteConnection(), "field", autoClearDelay: TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CopyVaultFieldValueToClipboardCommandRejectsMissingFieldId(string? fieldId)
    {
        Assert.ThrowsAny<ArgumentException>(() => new CopyVaultFieldValueToClipboardCommand(new SqliteConnection(), fieldId!));
    }

    [Fact]
    public async Task MainWindowViewModelCopyFieldValueCommandSendsClipboardCommandForSelectedField()
    {
        DateTimeOffset timestamp = Timestamp;
        VaultNodeHierarchyRecord root = new("node-root", null, "Root", 0, timestamp, timestamp, Array.Empty<VaultNodeHierarchyRecord>());
        VaultFieldRecord field = CreateField("field-api-key", "node-root", "ApiKey", "secret-value");
        RecordingSender sender = new([root])
        {
            FieldsByNodeId = { ["node-root"] = new[] { field } }
        };
        MainWindowViewModel viewModel = new(sender);
        await using SqliteConnection connection = await CreateOpenConnectionAsync();
        await viewModel.LoadVaultTreeAsync(connection);
        await viewModel.SelectVaultNodeAsync(Assert.Single(viewModel.VaultNodes));
        VaultFieldViewModel selectedField = Assert.Single(viewModel.SelectedFields);

        await ((AsyncCommand)viewModel.CopyFieldValueCommand).ExecuteAsync(selectedField);

        CopyVaultFieldValueToClipboardCommand command = Assert.IsType<CopyVaultFieldValueToClipboardCommand>(sender.LastRequest);
        Assert.Same(connection, command.Connection);
        Assert.Equal("field-api-key", command.FieldId);
        Assert.True(command.AutoClearEnabled);
        Assert.Equal(CopyVaultFieldValueToClipboardCommand.DefaultAutoClearDelay, command.AutoClearDelay);
    }

    private static async Task<SqliteConnection> CreateOpenConnectionAsync()
    {
        SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static VaultFieldRecord CreateField(string id, string nodeId, string key, string value)
    {
        return new VaultFieldRecord(id, nodeId, key, Encoding.UTF8.GetBytes(value), 0, Timestamp, Timestamp);
    }

    private sealed class RecordingClipboardService : IClipboardService
    {
        public string? Text { get; private set; }

        public Task SetTextAsync(string text, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Text = text;
            return Task.CompletedTask;
        }

        public Task<string?> GetTextAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Text);
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Text = null;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingClipboardAutoClearService : IClipboardAutoClearService
    {
        public string? ScheduledText { get; private set; }

        public TimeSpan? ScheduledDelay { get; private set; }

        public bool CancelPendingClearCalled { get; private set; }

        public Task ScheduleClearAsync(string copiedText, TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ScheduledText = copiedText;
            ScheduledDelay = delay;
            return Task.CompletedTask;
        }

        public Task CancelPendingClearAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CancelPendingClearCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class ControllableClipboardClearDelay : IClipboardClearDelay
    {
        private readonly TaskCompletionSource delayRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource delayCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            delayRequested.TrySetResult();
            return delayCompleted.Task.WaitAsync(cancellationToken);
        }

        public Task WaitForDelayRequestAsync()
        {
            return delayRequested.Task;
        }

        public void CompleteDelay()
        {
            delayCompleted.TrySetResult();
        }
    }

    private sealed class RecordingFieldReader : IVaultFieldReader
    {
        private readonly VaultFieldRecord? field;

        public RecordingFieldReader(VaultFieldRecord? field)
        {
            this.field = field;
        }

        public DbConnection? LastConnection { get; private set; }

        public GetVaultFieldByIdQuery? LastQuery { get; private set; }

        public Task<VaultFieldRecord?> GetByIdAsync(DbConnection connection, GetVaultFieldByIdQuery query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastConnection = connection;
            LastQuery = query;
            return Task.FromResult(field);
        }

        public Task<IReadOnlyList<VaultFieldRecord>> ListByNodeIdAsync(DbConnection connection, ListVaultFieldsByNodeIdQuery query, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Clipboard tests do not list fields through this reader.");
        }

        public Task<IReadOnlyList<VaultSearchResultRecord>> SearchAsync(DbConnection connection, SearchVaultQuery query, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Clipboard tests do not search fields through this reader.");
        }
    }

    private sealed class RecordingSender : ISender
    {
        public RecordingSender(IReadOnlyList<VaultNodeHierarchyRecord> hierarchy)
        {
            Hierarchy = hierarchy;
        }

        public IReadOnlyList<VaultNodeHierarchyRecord> Hierarchy { get; }

        public Dictionary<string, IReadOnlyList<VaultFieldRecord>> FieldsByNodeId { get; } = new(StringComparer.Ordinal);

        public object? LastRequest { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            if (request is ListVaultNodeHierarchyQuery && Hierarchy is TResponse hierarchyResponse)
            {
                return Task.FromResult(hierarchyResponse);
            }

            if (request is ListVaultFieldsByNodeIdQuery fieldQuery)
            {
                IReadOnlyList<VaultFieldRecord> fields = FieldsByNodeId.GetValueOrDefault(fieldQuery.NodeId, Array.Empty<VaultFieldRecord>());
                if (fields is TResponse fieldResponse)
                {
                    return Task.FromResult(fieldResponse);
                }
            }

            throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.");
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            LastRequest = request;
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            if (request is ListVaultNodeHierarchyQuery)
            {
                return Task.FromResult<object?>(Hierarchy);
            }

            if (request is ListVaultFieldsByNodeIdQuery fieldQuery)
            {
                return Task.FromResult<object?>(FieldsByNodeId.GetValueOrDefault(fieldQuery.NodeId, Array.Empty<VaultFieldRecord>()));
            }

            if (request is CopyVaultFieldValueToClipboardCommand)
            {
                return Task.FromResult<object?>(null);
            }

            throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.");
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Clipboard tests do not use streaming requests.");
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Clipboard tests do not use streaming requests.");
        }
    }
}
