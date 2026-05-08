using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Qps;

public sealed class ReadQpsVaultFileQuery : IQuery<QpsVaultFile>
{
    public ReadQpsVaultFileQuery(string filePath)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    public string FilePath { get; }
}
