namespace StructVault.Application.Abstractions.Storage;

public interface IQpsFileBackupService
{
    Task BackupAsync(string filePath, CancellationToken cancellationToken);

    Task RestoreAsync(string filePath, CancellationToken cancellationToken);
}
