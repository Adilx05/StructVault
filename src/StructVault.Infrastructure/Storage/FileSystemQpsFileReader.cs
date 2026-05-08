using StructVault.Application.Abstractions.Storage;

namespace StructVault.Infrastructure.Storage;

public sealed class FileSystemQpsFileReader : IQpsFileReader
{
    public async Task<byte[]> ReadAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        cancellationToken.ThrowIfCancellationRequested();

        string fullPath = Path.GetFullPath(filePath);
        return await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
    }
}
