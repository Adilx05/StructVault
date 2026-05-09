using System.Data.Common;
using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Persistence;

public sealed class SaveIdleLockSettingsCommand : ICommand
{
    public SaveIdleLockSettingsCommand(DbConnection connection, bool isEnabled, TimeSpan timeout)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Idle lock timeout must be greater than zero.");
        }

        if (timeout.TotalSeconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Idle lock timeout is too large.");
        }

        if (timeout != TimeSpan.FromSeconds((int)timeout.TotalSeconds))
        {
            throw new ArgumentException("Idle lock timeout must be specified in whole seconds.", nameof(timeout));
        }

        IsEnabled = isEnabled;
        Timeout = timeout;
    }

    public DbConnection Connection { get; }

    public bool IsEnabled { get; }

    public TimeSpan Timeout { get; }
}
