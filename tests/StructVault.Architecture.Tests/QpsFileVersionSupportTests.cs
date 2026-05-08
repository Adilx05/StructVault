using StructVault.Application.Qps;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class QpsFileVersionSupportTests
{
    [Fact]
    public void FileFormatExposesCurrentSupportedVersion()
    {
        QpsFileVersionSupport support = QpsFileFormat.VersionSupport;

        Assert.Equal(QpsFileFormat.CurrentVersion, support.CurrentVersion);
        Assert.Equal(new[] { QpsFileFormat.CurrentVersion }, support.SupportedVersions);
        Assert.Equal(QpsFileFormat.CurrentVersion, support.OldestSupportedVersion);
        Assert.Equal(QpsFileFormat.CurrentVersion, support.NewestSupportedVersion);
        Assert.True(support.IsSupported(QpsFileFormat.CurrentVersion));
        Assert.True(QpsFileFormat.IsSupportedVersion(QpsFileFormat.CurrentVersion));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(byte.MaxValue)]
    public void FileFormatRejectsUnsupportedVersions(byte unsupportedVersion)
    {
        Assert.False(QpsFileFormat.IsSupportedVersion(unsupportedVersion));

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            QpsFileFormat.EnsureSupportedVersion(unsupportedVersion));

        Assert.Contains($"QPS file version {unsupportedVersion} is not supported", exception.Message);
        Assert.Contains(QpsFileFormat.CurrentVersion.ToString(), exception.Message);
    }

    [Fact]
    public async Task VersionSupportQueryReturnsSupportedVersionMetadata()
    {
        GetQpsFileVersionSupportQueryHandler handler = new();

        QpsFileVersionSupport support = await handler.Handle(new GetQpsFileVersionSupportQuery(), CancellationToken.None);

        Assert.Equal(QpsFileFormat.CurrentVersion, support.CurrentVersion);
        Assert.Equal(new[] { QpsFileFormat.CurrentVersion }, support.SupportedVersions);
    }

    [Fact]
    public async Task VersionSupportQueryHonorsCancellationBeforeReturningMetadata()
    {
        GetQpsFileVersionSupportQueryHandler handler = new();
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await handler.Handle(new GetQpsFileVersionSupportQuery(), cancellation.Token));
    }

    [Fact]
    public void VersionSupportRequiresAtLeastOneSupportedVersion()
    {
        Assert.Throws<ArgumentException>(() => new QpsFileVersionSupport(QpsFileFormat.CurrentVersion, Array.Empty<byte>()));
    }

    [Fact]
    public void VersionSupportRequiresCurrentVersionToBeSupported()
    {
        Assert.Throws<ArgumentException>(() => new QpsFileVersionSupport(QpsFileFormat.CurrentVersion, new byte[] { 2 }));
    }

    [Fact]
    public void VersionSupportNormalizesSupportedVersions()
    {
        QpsFileVersionSupport support = new(2, new byte[] { 2, 1, 2 });

        Assert.Equal(new byte[] { 1, 2 }, support.SupportedVersions);
        Assert.Equal(1, support.OldestSupportedVersion);
        Assert.Equal(2, support.NewestSupportedVersion);
    }
}
