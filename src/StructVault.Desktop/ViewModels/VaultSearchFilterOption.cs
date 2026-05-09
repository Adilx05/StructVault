using StructVault.Application.Persistence;

namespace StructVault.Desktop.ViewModels;

public sealed class VaultSearchFilterOption
{
    public VaultSearchFilterOption(SearchVaultFilter value, string displayName)
    {
        if (!Enum.IsDefined(typeof(SearchVaultFilter), value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Vault search filter option is not supported.");
        }

        Value = value;
        DisplayName = RequireNonEmpty(displayName, nameof(displayName));
    }

    public SearchVaultFilter Value { get; }

    public string DisplayName { get; }

    private static string RequireNonEmpty(string value, string parameterName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException("Vault search filter display name cannot be empty or whitespace.", parameterName);
        }

        return normalizedValue;
    }
}
