using MediatR;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Application.Persistence;

public sealed class ListVaultNodesQueryHandler : IRequestHandler<ListVaultNodesQuery, IReadOnlyList<VaultNodeRecord>>
{
    private readonly IVaultNodeReader nodeReader;

    public ListVaultNodesQueryHandler(IVaultNodeReader nodeReader)
    {
        this.nodeReader = nodeReader ?? throw new ArgumentNullException(nameof(nodeReader));
    }

    public async Task<IReadOnlyList<VaultNodeRecord>> Handle(ListVaultNodesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return await nodeReader.ListAsync(request.Connection, request, cancellationToken).ConfigureAwait(false);
    }
}
