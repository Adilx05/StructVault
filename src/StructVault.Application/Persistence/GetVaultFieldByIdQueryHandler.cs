using MediatR;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Application.Persistence;

public sealed class GetVaultFieldByIdQueryHandler : IRequestHandler<GetVaultFieldByIdQuery, VaultFieldRecord?>
{
    private readonly IVaultFieldReader fieldReader;

    public GetVaultFieldByIdQueryHandler(IVaultFieldReader fieldReader)
    {
        this.fieldReader = fieldReader ?? throw new ArgumentNullException(nameof(fieldReader));
    }

    public async Task<VaultFieldRecord?> Handle(GetVaultFieldByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return await fieldReader.GetByIdAsync(request.Connection, request, cancellationToken).ConfigureAwait(false);
    }
}
