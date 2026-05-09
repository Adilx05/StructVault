using MediatR;
using StructVault.Application.Abstractions.Persistence;
using StructVault.Application.Abstractions.Security;
using StructVault.Application.Abstractions.Storage;
using StructVault.Application.Errors;

namespace StructVault.Application.Qps;

public sealed class TrySaveQpsVaultFileCommandHandler : IRequestHandler<TrySaveQpsVaultFileCommand, VaultOperationResult>
{
    private readonly SaveQpsVaultFileCommandHandler saveHandler;

    public TrySaveQpsVaultFileCommandHandler(
        IVaultDatabaseSerializer databaseSerializer,
        IKeyDerivationService keyDerivationService,
        IAuthenticatedEncryptionService encryptionService,
        IQpsFileBackupService backupService,
        IQpsFileWriter fileWriter)
    {
        saveHandler = new SaveQpsVaultFileCommandHandler(
            databaseSerializer ?? throw new ArgumentNullException(nameof(databaseSerializer)),
            keyDerivationService ?? throw new ArgumentNullException(nameof(keyDerivationService)),
            encryptionService ?? throw new ArgumentNullException(nameof(encryptionService)),
            backupService ?? throw new ArgumentNullException(nameof(backupService)),
            fileWriter ?? throw new ArgumentNullException(nameof(fileWriter)));
    }

    public async Task<VaultOperationResult> Handle(TrySaveQpsVaultFileCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await saveHandler.Handle(
                    new SaveQpsVaultFileCommand(request.Connection, request.FilePath, request.Password),
                    cancellationToken)
                .ConfigureAwait(false);

            return VaultOperationResult.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return VaultOperationResult.Failure(VaultOperationErrorMapper.FromException(ex, "saved"));
        }
    }
}
