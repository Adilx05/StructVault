using MediatR;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Application.Persistence;

public sealed class SearchVaultQueryHandler : IRequestHandler<SearchVaultQuery, IReadOnlyList<VaultSearchResultRecord>>
{
    private readonly IVaultNodeReader nodeReader;
    private readonly IVaultFieldReader fieldReader;

    public SearchVaultQueryHandler(IVaultNodeReader nodeReader, IVaultFieldReader fieldReader)
    {
        this.nodeReader = nodeReader ?? throw new ArgumentNullException(nameof(nodeReader));
        this.fieldReader = fieldReader ?? throw new ArgumentNullException(nameof(fieldReader));
    }

    public async Task<IReadOnlyList<VaultSearchResultRecord>> Handle(SearchVaultQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return request.Filter switch
        {
            SearchVaultFilter.All => await SearchNodesAndFieldsAsync(request, cancellationToken).ConfigureAwait(false),
            SearchVaultFilter.Nodes => await nodeReader.SearchAsync(request.Connection, request, cancellationToken).ConfigureAwait(false),
            SearchVaultFilter.Fields => await fieldReader.SearchAsync(request.Connection, request, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported vault search filter '{request.Filter}'.")
        };
    }

    private async Task<IReadOnlyList<VaultSearchResultRecord>> SearchNodesAndFieldsAsync(SearchVaultQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<VaultSearchResultRecord> nodeResults = await nodeReader
            .SearchAsync(request.Connection, request, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<VaultSearchResultRecord> fieldResults = await fieldReader
            .SearchAsync(request.Connection, request, cancellationToken)
            .ConfigureAwait(false);

        List<VaultSearchResultRecord> results = new(nodeResults.Count + fieldResults.Count);
        results.AddRange(nodeResults);
        results.AddRange(fieldResults);
        return results;
    }
}
