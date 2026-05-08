using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Qps;

public sealed class ParseQpsVaultFileHeaderQuery : IQuery<QpsHeader>
{
    private readonly byte[] fileBytes;

    public ParseQpsVaultFileHeaderQuery(byte[] fileBytes)
    {
        this.fileBytes = fileBytes?.ToArray() ?? throw new ArgumentNullException(nameof(fileBytes));
    }

    public ReadOnlyMemory<byte> FileBytes => fileBytes;
}
