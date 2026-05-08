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
        QpsHeader header = QpsFileFormat.ReadHeader(fileBytes);
        Validate(header);

        long requiredLength =
            QpsFileFormat.HeaderSizeInBytes +
            header.SaltLength +
            header.InitializationVectorLength +
            header.CiphertextLength +
            header.AuthenticationTagLength;

        if (fileBytes.Length != requiredLength)
        {
            throw new ArgumentException("QPS data length does not match the header metadata.", nameof(qpsFileBytes));
        }

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

    private static void Validate(QpsHeader header)
    {
        if (header.SaltLength < QpsFileFormat.MinimumSaltSizeInBytes)
        {
            throw new ArgumentException(
                $"QPS header salt length must be at least {QpsFileFormat.MinimumSaltSizeInBytes} bytes.",
                nameof(header));
        }

        if (header.InitializationVectorLength != QpsFileFormat.InitializationVectorSizeInBytes)
        {
            throw new ArgumentException(
                $"QPS header initialization vector length must be {QpsFileFormat.InitializationVectorSizeInBytes} bytes.",
                nameof(header));
        }

        if (header.AuthenticationTagLength != QpsFileFormat.AuthenticationTagSizeInBytes)
        {
            throw new ArgumentException(
                $"QPS header authentication tag length must be {QpsFileFormat.AuthenticationTagSizeInBytes} bytes.",
                nameof(header));
        }

        if (header.CiphertextLength <= 0)
        {
            throw new ArgumentException("QPS header must reference encrypted vault data.", nameof(header));
        }

        if (header.CiphertextLength > int.MaxValue)
        {
            throw new ArgumentException("QPS encrypted data length exceeds the supported in-memory limit.", nameof(header));
        }
    }
}
