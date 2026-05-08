using System.Collections.ObjectModel;

namespace StructVault.Application.Qps;

public sealed class QpsFileVersionSupport
{
    public QpsFileVersionSupport(byte currentVersion, IReadOnlyCollection<byte> supportedVersions)
    {
        ArgumentNullException.ThrowIfNull(supportedVersions);

        if (supportedVersions.Count == 0)
        {
            throw new ArgumentException("At least one QPS file version must be supported.", nameof(supportedVersions));
        }

        byte[] versions = supportedVersions
            .Distinct()
            .OrderBy(version => version)
            .ToArray();

        if (!versions.Contains(currentVersion))
        {
            throw new ArgumentException("The current QPS file version must be listed as supported.", nameof(currentVersion));
        }

        CurrentVersion = currentVersion;
        SupportedVersions = new ReadOnlyCollection<byte>(versions);
    }

    public byte CurrentVersion { get; }

    public IReadOnlyList<byte> SupportedVersions { get; }

    public byte OldestSupportedVersion => SupportedVersions[0];

    public byte NewestSupportedVersion => SupportedVersions[^1];

    public bool IsSupported(byte version)
    {
        return SupportedVersions.Contains(version);
    }
}
