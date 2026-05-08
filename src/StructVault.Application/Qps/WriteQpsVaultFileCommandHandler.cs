using MediatR;
using StructVault.Application.Abstractions.Storage;

namespace StructVault.Application.Qps;

public sealed class WriteQpsVaultFileCommandHandler : IRequestHandler<WriteQpsVaultFileCommand>
{
    private readonly IQpsFileWriter fileWriter;

    public WriteQpsVaultFileCommandHandler(IQpsFileWriter fileWriter)
    {
        this.fileWriter = fileWriter ?? throw new ArgumentNullException(nameof(fileWriter));
    }

    public async Task Handle(WriteQpsVaultFileCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(request);

        await fileWriter.WriteAsync(request.FilePath, request.FileBytes, cancellationToken).ConfigureAwait(false);
    }

    private static void Validate(WriteQpsVaultFileCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            throw new ArgumentException("A QPS vault file path is required.", nameof(request));
        }

        ReadOnlySpan<byte> fileBytes = request.FileBytes.Span;
        QpsHeader header = QpsFileFormat.ReadHeader(fileBytes);

        if (header.SaltLength < QpsFileFormat.MinimumSaltSizeInBytes)
        {
            throw new ArgumentException(
                $"QPS header salt length must be at least {QpsFileFormat.MinimumSaltSizeInBytes} bytes.",
                nameof(request));
        }

        if (header.InitializationVectorLength != QpsFileFormat.InitializationVectorSizeInBytes)
        {
            throw new ArgumentException(
                $"QPS header initialization vector length must be {QpsFileFormat.InitializationVectorSizeInBytes} bytes.",
                nameof(request));
        }

        if (header.AuthenticationTagLength != QpsFileFormat.AuthenticationTagSizeInBytes)
        {
            throw new ArgumentException(
                $"QPS header authentication tag length must be {QpsFileFormat.AuthenticationTagSizeInBytes} bytes.",
                nameof(request));
        }

        if (header.CiphertextLength <= 0)
        {
            throw new ArgumentException("QPS header must reference encrypted vault data.", nameof(request));
        }

        long requiredLength =
            QpsFileFormat.HeaderSizeInBytes +
            header.SaltLength +
            header.InitializationVectorLength +
            header.CiphertextLength +
            header.AuthenticationTagLength;

        if (fileBytes.Length != requiredLength)
        {
            throw new ArgumentException("QPS data length does not match the header metadata.", nameof(request));
        }
    }
}
