using StructVault.Application.Abstractions.Storage;

namespace StructVault.Infrastructure.Storage;

public sealed class FileSystemQpsFileWriter : IQpsFileWriter
{
    public async Task WriteAsync(string filePath, ReadOnlyMemory<byte> fileBytes, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (fileBytes.IsEmpty)
        {
            throw new ArgumentException("QPS vault file bytes are required.", nameof(fileBytes));
        }

        cancellationToken.ThrowIfCancellationRequested();

        string fullPath = Path.GetFullPath(filePath);
        string? directoryPath = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        string tempFilePath = CreateTemporaryPath(fullPath);

        try
        {
            await using (FileStream stream = new(
                tempFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(fileBytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempFilePath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    private static string CreateTemporaryPath(string fullPath)
    {
        string directoryPath = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        string fileName = Path.GetFileName(fullPath);
        string tempFileName = $".{fileName}.{Guid.NewGuid():N}.tmp";

        return Path.Combine(directoryPath, tempFileName);
    }
}
