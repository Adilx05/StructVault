using MediatR;
using StructVault.Application.Abstractions.Storage;

namespace StructVault.Application.Qps;

public sealed class ReadQpsVaultFileQueryHandler : IRequestHandler<ReadQpsVaultFileQuery, QpsVaultFile>
{
    private readonly IQpsFileReader fileReader;

    public ReadQpsVaultFileQueryHandler(IQpsFileReader fileReader)
    {
        this.fileReader = fileReader ?? throw new ArgumentNullException(nameof(fileReader));
    }

    public async Task<QpsVaultFile> Handle(ReadQpsVaultFileQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(request);

        byte[] fileBytes = await fileReader.ReadAsync(request.FilePath, cancellationToken).ConfigureAwait(false);
        if (fileBytes.Length == 0)
        {
            throw new ArgumentException("QPS vault file is empty.", nameof(request));
        }

        return ParseQpsVaultFileQueryHandler.Parse(fileBytes);
    }

    private static void Validate(ReadQpsVaultFileQuery request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            throw new ArgumentException("A QPS vault file path is required.", nameof(request));
        }
    }
}
