namespace StructVault.Application.Persistence;

public sealed record IdleLockSettingsRecord(bool IsEnabled, TimeSpan Timeout)
{
    public static IdleLockSettingsRecord Default { get; } = new(true, TimeSpan.FromMinutes(15));
}
