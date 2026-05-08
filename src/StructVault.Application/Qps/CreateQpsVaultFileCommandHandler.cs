using MediatR;

namespace StructVault.Application.Qps;

public sealed class CreateQpsVaultFileCommandHandler : IRequestHandler<CreateQpsVaultFileCommand, byte[]>
{
    public Task<byte[]> Handle(CreateQpsVaultFileCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(request);

        QpsHeader header = new(
            QpsFileFormat.CurrentVersion,
            request.Salt.Length,
            request.InitializationVector.Length,
            request.AuthenticationTag.Length,
            request.Ciphertext.Length);

        int fileLength = checked(
            QpsFileFormat.HeaderSizeInBytes +
            request.Salt.Length +
            request.InitializationVector.Length +
            request.Ciphertext.Length +
            request.AuthenticationTag.Length);

        byte[] fileBytes = new byte[fileLength];
        Span<byte> destination = fileBytes;
        QpsFileFormat.WriteHeader(destination[..QpsFileFormat.HeaderSizeInBytes], header);

        int offset = QpsFileFormat.HeaderSizeInBytes;
        request.Salt.Span.CopyTo(destination[offset..]);
        offset += request.Salt.Length;
        request.InitializationVector.Span.CopyTo(destination[offset..]);
        offset += request.InitializationVector.Length;
        request.Ciphertext.Span.CopyTo(destination[offset..]);
        offset += request.Ciphertext.Length;
        request.AuthenticationTag.Span.CopyTo(destination[offset..]);

        return Task.FromResult(fileBytes);
    }

    private static void Validate(CreateQpsVaultFileCommand request)
    {
        if (request.Salt.Length < QpsFileFormat.MinimumSaltSizeInBytes)
        {
            throw new ArgumentException(
                $"QPS files require an Argon2 salt of at least {QpsFileFormat.MinimumSaltSizeInBytes} bytes.",
                nameof(request));
        }

        if (request.Salt.Length > ushort.MaxValue)
        {
            throw new ArgumentException("QPS salt length exceeds the file format limit.", nameof(request));
        }

        if (request.InitializationVector.Length != QpsFileFormat.InitializationVectorSizeInBytes)
        {
            throw new ArgumentException(
                $"QPS files require a {QpsFileFormat.InitializationVectorSizeInBytes}-byte AES-GCM initialization vector.",
                nameof(request));
        }

        if (request.AuthenticationTag.Length != QpsFileFormat.AuthenticationTagSizeInBytes)
        {
            throw new ArgumentException(
                $"QPS files require a {QpsFileFormat.AuthenticationTagSizeInBytes}-byte AES-GCM authentication tag.",
                nameof(request));
        }

        if (request.Ciphertext.IsEmpty)
        {
            throw new ArgumentException("QPS files require encrypted vault data.", nameof(request));
        }
    }
}
