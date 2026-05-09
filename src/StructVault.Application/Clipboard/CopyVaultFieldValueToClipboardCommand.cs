using System.Data.Common;
using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Clipboard;

public sealed class CopyVaultFieldValueToClipboardCommand : ICommand
{
    public static readonly TimeSpan DefaultAutoClearDelay = TimeSpan.FromSeconds(30);

    public CopyVaultFieldValueToClipboardCommand(
        DbConnection connection,
        string fieldId,
        bool autoClearEnabled = true,
        TimeSpan? autoClearDelay = null)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        FieldId = RequireNonEmpty(fieldId, nameof(fieldId));
        AutoClearEnabled = autoClearEnabled;
        AutoClearDelay = RequireValidDelay(autoClearDelay ?? DefaultAutoClearDelay, nameof(autoClearDelay));
    }

    public DbConnection Connection { get; }

    public string FieldId { get; }

    public bool AutoClearEnabled { get; }

    public TimeSpan AutoClearDelay { get; }

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

    private static TimeSpan RequireValidDelay(TimeSpan delay, string parameterName)
    {
        if (delay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName, delay, "Clipboard auto-clear delay must be greater than zero.");
        }

        return delay;
    }
}
