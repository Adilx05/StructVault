using System.Data.Common;
using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Persistence;

public sealed class SaveClipboardSettingsCommand : ICommand
{
    public SaveClipboardSettingsCommand(DbConnection connection, bool autoClearEnabled, TimeSpan autoClearDelay)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        if (autoClearDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(autoClearDelay), autoClearDelay, "Clipboard auto-clear delay must be greater than zero.");
        }

        if (autoClearDelay.TotalSeconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(autoClearDelay), autoClearDelay, "Clipboard auto-clear delay is too large.");
        }

        if (autoClearDelay != TimeSpan.FromSeconds((int)autoClearDelay.TotalSeconds))
        {
            throw new ArgumentException("Clipboard auto-clear delay must be specified in whole seconds.", nameof(autoClearDelay));
        }

        AutoClearEnabled = autoClearEnabled;
        AutoClearDelay = autoClearDelay;
    }

    public DbConnection Connection { get; }

    public bool AutoClearEnabled { get; }

    public TimeSpan AutoClearDelay { get; }
}
