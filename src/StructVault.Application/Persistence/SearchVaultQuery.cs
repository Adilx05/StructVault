using System.Data.Common;
using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Persistence;

public sealed class SearchVaultQuery : IQuery<IReadOnlyList<VaultSearchResultRecord>>
{
    public SearchVaultQuery(DbConnection connection, string searchText)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        SearchText = RequireNonEmpty(searchText, nameof(searchText));
    }

    public DbConnection Connection { get; }

    public string SearchText { get; }

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
