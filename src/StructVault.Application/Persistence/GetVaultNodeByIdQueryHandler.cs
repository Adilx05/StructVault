using MediatR;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Application.Persistence;

public sealed class GetVaultNodeByIdQueryHandler : IRequestHandler<GetVaultNodeByIdQuery, VaultNodeRecord?>
{
    private readonly IVaultNodeReader nodeReader;

    public GetVaultNodeByIdQueryHandler(IVaultNodeReader nodeReader)
    {
        this.nodeReader = nodeReader ?? throw new ArgumentNullException(nameof(nodeReader));
    }

    public async Task<VaultNodeRecord?> Handle(GetVaultNodeByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return await nodeReader.GetByIdAsync(request.Connection, request, cancellationToken).ConfigureAwait(false);
    }
}
