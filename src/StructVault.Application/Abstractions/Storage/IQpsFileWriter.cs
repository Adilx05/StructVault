namespace StructVault.Application.Abstractions.Storage;

public interface IQpsFileWriter
{
    Task WriteAsync(string filePath, ReadOnlyMemory<byte> fileBytes, CancellationToken cancellationToken);
}
