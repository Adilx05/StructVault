using System.Data.Common;
using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Clipboard;

public sealed class CopyVaultFieldValueToClipboardCommand : ICommand
{
    public CopyVaultFieldValueToClipboardCommand(DbConnection connection, string fieldId)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        FieldId = RequireNonEmpty(fieldId, nameof(fieldId));
    }

    public DbConnection Connection { get; }

    public string FieldId { get; }

    private static string RequireNonEmpty(string value, string parameterName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException("Value cannot be empty or whitespace.", parameterName);
        }

        return normalizedValue;
    }
}
