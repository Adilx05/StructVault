using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Qps;

public sealed class ParseQpsVaultFileQuery : IQuery<QpsVaultFile>
{
    private readonly byte[] fileBytes;

    public ParseQpsVaultFileQuery(byte[] fileBytes)
    {
        this.fileBytes = fileBytes?.ToArray() ?? throw new ArgumentNullException(nameof(fileBytes));
    }

    public ReadOnlyMemory<byte> FileBytes => fileBytes;
}
