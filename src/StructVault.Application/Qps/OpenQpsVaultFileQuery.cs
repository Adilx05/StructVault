using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Qps;

public sealed class OpenQpsVaultFileQuery : IQuery<byte[]>
{
    public OpenQpsVaultFileQuery(string filePath, string password)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        Password = password ?? throw new ArgumentNullException(nameof(password));
    }

    public string FilePath { get; }

    public string Password { get; }
}
