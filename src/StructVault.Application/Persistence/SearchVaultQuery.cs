using System.Data.Common;
using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Persistence;

public sealed class SearchVaultQuery : IQuery<IReadOnlyList<VaultSearchResultRecord>>
{
    public SearchVaultQuery(DbConnection connection, string searchText, SearchVaultFilter filter = SearchVaultFilter.All)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        SearchText = RequireNonEmpty(searchText, nameof(searchText));
        Filter = RequireSupportedFilter(filter, nameof(filter));
    }

    public DbConnection Connection { get; }

    public string SearchText { get; }

    public SearchVaultFilter Filter { get; }

    private static SearchVaultFilter RequireSupportedFilter(SearchVaultFilter filter, string parameterName)
    {
        if (!Enum.IsDefined(typeof(SearchVaultFilter), filter))
        {
            throw new ArgumentOutOfRangeException(parameterName, filter, "Vault search filter is not supported.");
        }

        return filter;
    }

    private static string RequireNonEmpty(string value, string parameterName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException("Vault search text cannot be empty or whitespace.", parameterName);
        }

        return normalizedValue;
    }
}
