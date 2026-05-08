using MediatR;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Application.Persistence;

public sealed class ListVaultFieldsByNodeIdQueryHandler : IRequestHandler<ListVaultFieldsByNodeIdQuery, IReadOnlyList<VaultFieldRecord>>
{
    private readonly IVaultFieldReader fieldReader;

    public ListVaultFieldsByNodeIdQueryHandler(IVaultFieldReader fieldReader)
    {
        this.fieldReader = fieldReader ?? throw new ArgumentNullException(nameof(fieldReader));
    }

    public async Task<IReadOnlyList<VaultFieldRecord>> Handle(ListVaultFieldsByNodeIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return await fieldReader.ListByNodeIdAsync(request.Connection, request, cancellationToken).ConfigureAwait(false);
    }
}
