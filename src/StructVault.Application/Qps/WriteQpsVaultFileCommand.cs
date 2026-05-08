using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Qps;

public sealed class WriteQpsVaultFileCommand : ICommand
{
    private readonly byte[] fileBytes;

    public WriteQpsVaultFileCommand(string filePath, byte[] fileBytes)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        this.fileBytes = fileBytes?.ToArray() ?? throw new ArgumentNullException(nameof(fileBytes));
    }

    public string FilePath { get; }

    public ReadOnlyMemory<byte> FileBytes => fileBytes;
}
