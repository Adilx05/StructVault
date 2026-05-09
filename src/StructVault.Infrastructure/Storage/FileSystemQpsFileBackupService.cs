using StructVault.Application.Abstractions.Storage;

namespace StructVault.Infrastructure.Storage;

public sealed class FileSystemQpsFileBackupService : IQpsFileBackupService
{
    public async Task BackupAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        cancellationToken.ThrowIfCancellationRequested();

        string fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            return;
        }

        string backupPath = CreateBackupPath(fullPath);
        await CopyFileAtomicallyAsync(fullPath, backupPath, cancellationToken).ConfigureAwait(false);
    }

    public async Task RestoreAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        cancellationToken.ThrowIfCancellationRequested();

        string fullPath = Path.GetFullPath(filePath);
        string backupPath = CreateBackupPath(fullPath);
        if (!File.Exists(backupPath))
        {
            throw new FileNotFoundException("The QPS vault backup file does not exist.", backupPath);
        }

        await CopyFileAtomicallyAsync(backupPath, fullPath, cancellationToken).ConfigureAwait(false);
    }

    public static string CreateBackupPath(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return Path.GetFullPath(filePath) + ".bak";
    }

    private static async Task CopyFileAtomicallyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        string? destinationDirectoryPath = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectoryPath))
        {
            Directory.CreateDirectory(destinationDirectoryPath);
        }

        string tempPath = CreateTemporaryPath(destinationPath);

        try
        {
            await using (FileStream sourceStream = new(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (FileStream destinationStream = new(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await sourceStream.CopyToAsync(destinationStream, cancellationToken).ConfigureAwait(false);
                await destinationStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static string CreateTemporaryPath(string destinationPath)
    {
        string directoryPath = Path.GetDirectoryName(destinationPath) ?? Directory.GetCurrentDirectory();
        string fileName = Path.GetFileName(destinationPath);
        string tempFileName = $".{fileName}.{Guid.NewGuid():N}.tmp";

        return Path.Combine(directoryPath, tempFileName);
    }
}
