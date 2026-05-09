using System.Text;
using StructVault.Application.Persistence;

namespace StructVault.Desktop.ViewModels;

public sealed class VaultFieldViewModel : ViewModelBase
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public VaultFieldViewModel(VaultFieldRecord field)
    {
        ArgumentNullException.ThrowIfNull(field);

        Id = RequireNonEmpty(field.Id, nameof(field));
        NodeId = RequireNonEmpty(field.NodeId, nameof(field));
        Key = RequireNonEmpty(field.Key, nameof(field));
        SortOrder = field.SortOrder;
        ValueLength = field.Value.Length;
        DisplayValue = CreateDisplayValue(field.Value);
    }

    public string Id { get; }

    public string NodeId { get; }

    public string Key { get; }

    public string DisplayValue { get; }

    public int ValueLength { get; }

    public int SortOrder { get; }

    private static string CreateDisplayValue(byte[] value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (value.Length == 0)
        {
            throw new ArgumentException("Vault field display values cannot be empty.", nameof(value));
        }

        try
        {
            return StrictUtf8.GetString(value);
        }
        catch (DecoderFallbackException)
        {
            return $"Binary value ({value.Length} bytes)";
        }
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
            throw new ArgumentException("Vault field values cannot be empty or whitespace.", parameterName);
        }

        return normalizedValue;
    }
}
