using MediatR;

namespace StructVault.Application.Qps;

public sealed class ParseQpsVaultFileQueryHandler : IRequestHandler<ParseQpsVaultFileQuery, QpsVaultFile>
{
    public Task<QpsVaultFile> Handle(ParseQpsVaultFileQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(Parse(request.FileBytes));
    }

    internal static QpsVaultFile Parse(ReadOnlyMemory<byte> qpsFileBytes)
    {
        ReadOnlySpan<byte> fileBytes = qpsFileBytes.Span;
        QpsHeader header = ParseQpsVaultFileHeaderQueryHandler.Parse(qpsFileBytes);

        int offset = QpsFileFormat.HeaderSizeInBytes;
        byte[] salt = fileBytes.Slice(offset, header.SaltLength).ToArray();
        offset += header.SaltLength;
        byte[] initializationVector = fileBytes.Slice(offset, header.InitializationVectorLength).ToArray();
        offset += header.InitializationVectorLength;
        byte[] ciphertext = fileBytes.Slice(offset, checked((int)header.CiphertextLength)).ToArray();
        offset += checked((int)header.CiphertextLength);
        byte[] authenticationTag = fileBytes.Slice(offset, header.AuthenticationTagLength).ToArray();

        return new QpsVaultFile(
            header.Version,
            salt,
            initializationVector,
            ciphertext,
            authenticationTag);
    }
}
