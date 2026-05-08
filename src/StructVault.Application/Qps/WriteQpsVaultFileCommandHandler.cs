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
        QpsFileFormat.ValidateFileLength(fileBytes, header, nameof(request));
    }
}
