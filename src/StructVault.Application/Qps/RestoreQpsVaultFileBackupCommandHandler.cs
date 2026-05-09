using MediatR;
using StructVault.Application.Abstractions.Storage;

namespace StructVault.Application.Qps;

public sealed class RestoreQpsVaultFileBackupCommandHandler : IRequestHandler<RestoreQpsVaultFileBackupCommand>
{
    private readonly IQpsFileBackupService backupService;

    public RestoreQpsVaultFileBackupCommandHandler(IQpsFileBackupService backupService)
    {
        this.backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
    }

    public async Task Handle(RestoreQpsVaultFileBackupCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(request.FilePath, nameof(request));

        await backupService.RestoreAsync(request.FilePath, cancellationToken).ConfigureAwait(false);
    }

    private static void Validate(string filePath, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A QPS vault file path is required.", parameterName);
        }
    }
}
