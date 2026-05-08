using MediatR;

namespace StructVault.Application.Qps;

public sealed class ParseQpsVaultFileHeaderQueryHandler : IRequestHandler<ParseQpsVaultFileHeaderQuery, QpsHeader>
{
    public Task<QpsHeader> Handle(ParseQpsVaultFileHeaderQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(Parse(request.FileBytes));
    }

    internal static QpsHeader Parse(ReadOnlyMemory<byte> qpsFileBytes)
    {
        ReadOnlySpan<byte> fileBytes = qpsFileBytes.Span;
        QpsHeader header = QpsFileFormat.ReadHeader(fileBytes);
        QpsFileFormat.ValidateFileLength(fileBytes, header, nameof(qpsFileBytes));

        return header;
    }
}
