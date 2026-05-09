using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.Data.Sqlite;
using StructVault.Application.Abstractions.IdleLock;
using StructVault.Application.Abstractions.Security;
using StructVault.Application.IdleLock;
using StructVault.Application.Persistence;
using StructVault.Application.Qps;
using StructVault.Application.Security;
using StructVault.Infrastructure.Security;
using StructVault.Infrastructure.Storage;
using StructVault.Desktop.ViewModels;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultIdleLockTests
{
    private const string VaultPassword = "correct horse battery staple";
    private static readonly DateTimeOffset InitialUtc = new(2026, 5, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] ValidSalt =
    [
        0x6A, 0x91, 0xD2, 0x48,
        0x03, 0x7C, 0xEF, 0xB5,
        0x2D, 0x84, 0x10, 0xA6,
        0x99, 0x3F, 0xC1, 0x5E,
    ];

    [Fact]
    public async Task LockVaultAfterIdleTimeoutCommandHandlerLocksWhenTimeoutIsReached()
    {
        ManualTimeProvider timeProvider = new(InitialUtc);
        IIdleActivityTracker tracker = new IdleActivityTracker(timeProvider);
        LockVaultAfterIdleTimeoutCommandHandler handler = new(tracker);

        VaultLockState state = await handler.Handle(
            new LockVaultAfterIdleTimeoutCommand(TimeSpan.FromMinutes(5), InitialUtc.AddMinutes(5)),
            CancellationToken.None);

        Assert.True(state.IsLocked);
        Assert.Equal(InitialUtc, state.LastActivityUtc);
        Assert.Equal(InitialUtc.AddMinutes(5), state.ObservedAtUtc);
        Assert.Equal(TimeSpan.FromMinutes(5), state.IdleDuration);
    }

    [Fact]
    public async Task LockVaultAfterIdleTimeoutCommandHandlerLeavesVaultUnlockedBeforeTimeout()
    {
        ManualTimeProvider timeProvider = new(InitialUtc);
        IIdleActivityTracker tracker = new IdleActivityTracker(timeProvider);
        LockVaultAfterIdleTimeoutCommandHandler handler = new(tracker);

        VaultLockState state = await handler.Handle(
            new LockVaultAfterIdleTimeoutCommand(TimeSpan.FromMinutes(5), InitialUtc.AddMinutes(4)),
            CancellationToken.None);

        Assert.False(state.IsLocked);
        Assert.Equal(TimeSpan.FromMinutes(4), state.IdleDuration);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void LockVaultAfterIdleTimeoutCommandRejectsNonPositiveTimeout(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LockVaultAfterIdleTimeoutCommand(TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MainWindowViewModelRejectsNonPositiveIdleLockTimeout(int seconds)
    {
        MainWindowViewModel viewModel = new(new RecordingSender(new VaultLockState(false, InitialUtc, InitialUtc, TimeSpan.Zero)));

        Assert.Throws<ArgumentOutOfRangeException>(() => viewModel.IdleLockTimeout = TimeSpan.FromSeconds(seconds));
    }

    [Fact]
    public async Task MainWindowViewModelLocksAndClearsVaultPresentationAfterIdleTimeout()
    {
        VaultNodeHierarchyRecord root = new("node-root", null, "Root", 0, InitialUtc, InitialUtc, Array.Empty<VaultNodeHierarchyRecord>());
        VaultFieldRecord field = new("field-password", "node-root", "Password", Encoding.UTF8.GetBytes("sensitive-value"), 0, InitialUtc, InitialUtc);
        RecordingSender sender = new(new VaultLockState(true, InitialUtc, InitialUtc.AddMinutes(15), TimeSpan.FromMinutes(15)), [root])
        {
            FieldsByNodeId = { ["node-root"] = new[] { field } }
        };
        MainWindowViewModel viewModel = new(sender);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        await viewModel.LoadVaultTreeAsync(connection, "vault.qps", "master-password");
        await viewModel.SelectVaultNodeAsync(Assert.Single(viewModel.VaultNodes));

        Assert.False(viewModel.IsVaultLocked);
        Assert.True(viewModel.CanSave);
        Assert.Single(viewModel.SelectedFields);

        bool locked = await viewModel.LockVaultAfterIdleTimeoutAsync();

        Assert.True(locked);
        Assert.True(viewModel.IsVaultLocked);
        Assert.Empty(viewModel.VaultNodes);
        Assert.Empty(viewModel.SelectedFields);
        Assert.Empty(viewModel.SearchResults);
        Assert.False(viewModel.HasSelectedNode);
        Assert.False(viewModel.CanSave);
        Assert.False(viewModel.AddRootNodeCommand.CanExecute(null));
        LockVaultAfterIdleTimeoutCommand command = Assert.Single(sender.Requests.OfType<LockVaultAfterIdleTimeoutCommand>());
        Assert.Equal(viewModel.IdleLockTimeout, command.IdleTimeout);
    }

    [Fact]
    public async Task MainWindowViewModelDoesNotLockBeforeIdleTimeout()
    {
        VaultNodeHierarchyRecord root = new("node-root", null, "Root", 0, InitialUtc, InitialUtc, Array.Empty<VaultNodeHierarchyRecord>());
        RecordingSender sender = new(new VaultLockState(false, InitialUtc, InitialUtc.AddMinutes(1), TimeSpan.FromMinutes(1)), [root]);
        MainWindowViewModel viewModel = new(sender);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        await viewModel.LoadVaultTreeAsync(connection, "vault.qps", VaultPassword);

        bool locked = await viewModel.LockVaultAfterIdleTimeoutAsync();

        Assert.False(locked);
        Assert.False(viewModel.IsVaultLocked);
        Assert.Single(viewModel.VaultNodes);
        Assert.Single(sender.Requests.OfType<LockVaultAfterIdleTimeoutCommand>());
    }

    [Fact]
    public async Task UnlockVaultCommandHandlerReturnsTrueForCorrectPassword()
    {
        string directoryPath = CreateUniqueTempDirectoryPath();
        string vaultFilePath = Path.Combine(directoryPath, "vault.qps");

        try
        {
            await WriteEncryptedVaultFileAsync(vaultFilePath, VaultPassword);
            UnlockVaultCommandHandler handler = new(
                new FileSystemQpsFileReader(),
                new Argon2idKeyDerivationService(),
                new Aes256GcmEncryptionService());

            bool unlocked = await handler.Handle(new UnlockVaultCommand(vaultFilePath, VaultPassword), CancellationToken.None);

            Assert.True(unlocked);
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Fact]
    public async Task UnlockVaultCommandHandlerReturnsFalseForIncorrectPassword()
    {
        string directoryPath = CreateUniqueTempDirectoryPath();
        string vaultFilePath = Path.Combine(directoryPath, "vault.qps");

        try
        {
            await WriteEncryptedVaultFileAsync(vaultFilePath, VaultPassword);
            UnlockVaultCommandHandler handler = new(
                new FileSystemQpsFileReader(),
                new Argon2idKeyDerivationService(),
                new Aes256GcmEncryptionService());

            bool unlocked = await handler.Handle(new UnlockVaultCommand(vaultFilePath, "incorrect password"), CancellationToken.None);

            Assert.False(unlocked);
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Fact]
    public async Task MainWindowViewModelPromptsForPasswordAndUnlocksWithCorrectPassword()
    {
        VaultNodeHierarchyRecord root = new("node-root", null, "Root", 0, InitialUtc, InitialUtc, Array.Empty<VaultNodeHierarchyRecord>());
        RecordingSender sender = new(new VaultLockState(true, InitialUtc, InitialUtc.AddMinutes(15), TimeSpan.FromMinutes(15)), [root])
        {
            UnlockResult = true
        };
        RecordingContextMenuInputService inputService = new() { Password = VaultPassword };
        MainWindowViewModel viewModel = new(sender, inputService);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await viewModel.LoadVaultTreeAsync(connection, "vault.qps", VaultPassword);
        await viewModel.LockVaultAfterIdleTimeoutAsync();

        bool unlocked = await viewModel.UnlockVaultAsync();

        Assert.True(unlocked);
        Assert.False(viewModel.IsVaultLocked);
        Assert.True(viewModel.CanSave);
        Assert.Single(viewModel.VaultNodes);
        Assert.Single(sender.Requests.OfType<UnlockVaultCommand>());
        Assert.Single(sender.Requests.OfType<RecordUserActivityCommand>());
        Assert.Empty(inputService.ValidationErrors);
    }

    [Fact]
    public async Task MainWindowViewModelKeepsVaultLockedWhenPasswordIsIncorrect()
    {
        VaultNodeHierarchyRecord root = new("node-root", null, "Root", 0, InitialUtc, InitialUtc, Array.Empty<VaultNodeHierarchyRecord>());
        RecordingSender sender = new(new VaultLockState(true, InitialUtc, InitialUtc.AddMinutes(15), TimeSpan.FromMinutes(15)), [root])
        {
            UnlockResult = false
        };
        RecordingContextMenuInputService inputService = new() { Password = "incorrect password" };
        MainWindowViewModel viewModel = new(sender, inputService);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await viewModel.LoadVaultTreeAsync(connection, "vault.qps", VaultPassword);
        await viewModel.LockVaultAfterIdleTimeoutAsync();

        bool unlocked = await viewModel.UnlockVaultAsync();

        Assert.False(unlocked);
        Assert.True(viewModel.IsVaultLocked);
        Assert.Empty(viewModel.VaultNodes);
        Assert.Single(sender.Requests.OfType<UnlockVaultCommand>());
        Assert.Contains(inputService.ValidationErrors, error => error.Title == "Unlock failed");
    }

    [Fact]
    public async Task MainWindowViewModelRejectsEmptyUnlockPasswordBeforeSendingCommand()
    {
        VaultNodeHierarchyRecord root = new("node-root", null, "Root", 0, InitialUtc, InitialUtc, Array.Empty<VaultNodeHierarchyRecord>());
        RecordingSender sender = new(new VaultLockState(true, InitialUtc, InitialUtc.AddMinutes(15), TimeSpan.FromMinutes(15)), [root]);
        RecordingContextMenuInputService inputService = new() { Password = " " };
        MainWindowViewModel viewModel = new(sender, inputService);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await viewModel.LoadVaultTreeAsync(connection, "vault.qps", VaultPassword);
        await viewModel.LockVaultAfterIdleTimeoutAsync();

        bool unlocked = await viewModel.UnlockVaultAsync();

        Assert.False(unlocked);
        Assert.True(viewModel.IsVaultLocked);
        Assert.Empty(sender.Requests.OfType<UnlockVaultCommand>());
        Assert.Contains(inputService.ValidationErrors, error => error.Title == "Unlock vault");
    }

    [Fact]
    public async Task MainWindowViewModelDoesNotLockVaultWithoutEncryptedSaveTarget()
    {
        VaultNodeHierarchyRecord root = new("node-root", null, "Root", 0, InitialUtc, InitialUtc, Array.Empty<VaultNodeHierarchyRecord>());
        RecordingSender sender = new(new VaultLockState(true, InitialUtc, InitialUtc.AddMinutes(15), TimeSpan.FromMinutes(15)), [root]);
        MainWindowViewModel viewModel = new(sender);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await viewModel.LoadVaultTreeAsync(connection);

        bool locked = await viewModel.LockVaultAfterIdleTimeoutAsync();

        Assert.False(locked);
        Assert.False(viewModel.IsVaultLocked);
        Assert.Single(viewModel.VaultNodes);
        Assert.Empty(sender.Requests.OfType<LockVaultAfterIdleTimeoutCommand>());
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow.ToUniversalTime();
        }

        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static async Task WriteEncryptedVaultFileAsync(string vaultFilePath, string password)
    {
        byte[] plaintextVaultData = Encoding.UTF8.GetBytes("StructVault unlock validation payload");
        byte[]? key = null;

        try
        {
            key = await new DeriveVaultKeyCommandHandler(new Argon2idKeyDerivationService())
                .Handle(new DeriveVaultKeyCommand(password, ValidSalt), CancellationToken.None);
            AesGcmEncryptionResult encryptionResult = await new EncryptVaultDataCommandHandler(new Aes256GcmEncryptionService())
                .Handle(new EncryptVaultDataCommand(plaintextVaultData, key), CancellationToken.None);
            byte[] qpsFileBytes = await new CreateQpsVaultFileCommandHandler()
                .Handle(
                    new CreateQpsVaultFileCommand(
                        ValidSalt,
                        encryptionResult.Nonce.ToArray(),
                        encryptionResult.Ciphertext.ToArray(),
                        encryptionResult.Tag.ToArray()),
                    CancellationToken.None);

            await new WriteQpsVaultFileCommandHandler(new FileSystemQpsFileWriter())
                .Handle(new WriteQpsVaultFileCommand(vaultFilePath, qpsFileBytes), CancellationToken.None);
        }
        finally
        {
            ZeroMemory(plaintextVaultData);
            if (key is not null)
            {
                ZeroMemory(key);
            }
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

    private static void ZeroMemory(byte[] bytes)
    {
        if (bytes.Length > 0)
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private sealed class RecordingSender : ISender
    {
        private readonly IReadOnlyList<VaultNodeHierarchyRecord> hierarchy;
        private readonly VaultLockState lockState;

        public RecordingSender(VaultLockState lockState, IReadOnlyList<VaultNodeHierarchyRecord>? hierarchy = null)
        {
            this.lockState = lockState;
            this.hierarchy = hierarchy ?? Array.Empty<VaultNodeHierarchyRecord>();
        }

        public List<object> Requests { get; } = new();

        public Dictionary<string, IReadOnlyList<VaultFieldRecord>> FieldsByNodeId { get; } = new(StringComparer.Ordinal);

        public bool UnlockResult { get; init; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);

            object response = request switch
            {
                ListVaultNodeHierarchyQuery => hierarchy,
                ListVaultFieldsByNodeIdQuery query => FieldsByNodeId.TryGetValue(query.NodeId, out IReadOnlyList<VaultFieldRecord>? fields)
                    ? fields
                    : Array.Empty<VaultFieldRecord>(),
                LockVaultAfterIdleTimeoutCommand => lockState,
                UnlockVaultCommand => UnlockResult,
                RecordUserActivityCommand => InitialUtc,
                _ => throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.")
            };

            return Task.FromResult((TResponse)response);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult<object?>(null);
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Idle lock tests do not use streaming requests.");
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Idle lock tests do not use streaming requests.");
        }
    }

    private sealed class RecordingContextMenuInputService : IContextMenuInputService
    {
        public string? Password { get; init; }

        public List<(string Title, string Message)> ValidationErrors { get; } = new();

        public string? RequestNodeName(string title, string message, string? initialName = null)
        {
            return null;
        }

        public VaultFieldInput? RequestField(string title, string keyMessage, string valueMessage, VaultFieldInput? initialValue = null)
        {
            return null;
        }

        public string? RequestPassword(string title, string message)
        {
            return Password;
        }

        public bool ConfirmDelete(string title, string message)
        {
            return false;
        }

        public UnsavedChangesExitChoice PromptUnsavedChangesOnExit(bool canSave)
        {
            return UnsavedChangesExitChoice.CancelExit;
        }

        public void ShowValidationError(string title, string message)
        {
            ValidationErrors.Add((title, message));
        }
    }
}
