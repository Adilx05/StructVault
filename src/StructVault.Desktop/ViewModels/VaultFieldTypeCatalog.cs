namespace StructVault.Desktop.ViewModels;

public static class VaultFieldTypeCatalog
{
    private static readonly string[] SensitiveKeyFragments =
    [
        "password",
        "passphrase",
        "secret",
        "token",
        "api key",
        "apikey",
        "access key",
        "private key",
        "signing key",
        "encryption key",
        "client secret",
        "credential",
        "pin",
        "otp",
        "mfa",
        "recovery code",
        "seed phrase"
    ];

    public static IReadOnlyList<VaultFieldTypeOption> EnterpriseFieldTypes { get; } =
    [
        new("Username", "Identity / Username"),
        new("Password", "Identity / Password", IsSensitive: true),
        new("Passphrase", "Identity / Passphrase", IsSensitive: true),
        new("Email", "Identity / Email"),
        new("Recovery Code", "Identity / Recovery code", IsSensitive: true),
        new("Website URL", "Network / Website URL"),
        new("Server Address", "Network / Server address"),
        new("Hostname", "Network / Hostname"),
        new("IP Address", "Network / IP address"),
        new("Port", "Network / Port"),
        new("Protocol", "Network / Protocol"),
        new("Database Name", "Database / Name"),
        new("Connection String", "Database / Connection string", IsSensitive: true),
        new("SSH Username", "SSH / Username"),
        new("SSH Private Key", "SSH / Private key", IsSensitive: true),
        new("SSH Passphrase", "SSH / Passphrase", IsSensitive: true),
        new("API Key", "API / Key", IsSensitive: true),
        new("API Secret", "API / Secret", IsSensitive: true),
        new("Bearer Token", "API / Bearer token", IsSensitive: true),
        new("Client ID", "OAuth / Client ID"),
        new("Client Secret", "OAuth / Client secret", IsSensitive: true),
        new("Tenant ID", "Cloud / Tenant ID"),
        new("Subscription ID", "Cloud / Subscription ID"),
        new("Access Key ID", "Cloud / Access key ID"),
        new("Secret Access Key", "Cloud / Secret access key", IsSensitive: true),
        new("Region", "Cloud / Region"),
        new("Environment", "Operations / Environment"),
        new("Owner", "Operations / Owner"),
        new("Runbook URL", "Operations / Runbook URL"),
        new("Notes", "General / Notes")
    ];

    public static bool IsSensitiveKey(string? key)
    {
        string normalizedKey = key?.Trim() ?? string.Empty;
        if (normalizedKey.Length == 0)
        {
            return false;
        }

        if (EnterpriseFieldTypes.Any(option =>
                option.IsSensitive && string.Equals(option.Key, normalizedKey, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return SensitiveKeyFragments.Any(fragment => normalizedKey.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }
}
