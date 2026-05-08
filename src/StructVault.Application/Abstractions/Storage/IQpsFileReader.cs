namespace StructVault.Application.Abstractions.Storage;

public interface IQpsFileReader
{
    Task<byte[]> ReadAsync(string filePath, CancellationToken cancellationToken);
}
