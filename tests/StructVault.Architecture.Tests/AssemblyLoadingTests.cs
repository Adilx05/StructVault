using System.Reflection;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class AssemblyLoadingTests
{
    public static IEnumerable<object[]> ExpectedAssemblyMarkers()
    {
        yield return [typeof(global::StructVault.Domain.AssemblyMarker), "StructVault.Domain"];
        yield return [typeof(global::StructVault.Application.AssemblyMarker), "StructVault.Application"];
        yield return [typeof(global::StructVault.Infrastructure.AssemblyMarker), "StructVault.Infrastructure"];
        yield return [typeof(global::StructVault.Persistence.AssemblyMarker), "StructVault.Persistence"];
        yield return [typeof(global::StructVault.Desktop.MainWindow), "StructVault.Desktop"];
    }

    [Theory]
    [MemberData(nameof(ExpectedAssemblyMarkers))]
    public void ExpectedAssembliesCanBeLoaded(Type markerType, string expectedAssemblyName)
    {
        Assembly assembly = markerType.Assembly;

        Assert.NotNull(assembly);
        Assert.Equal(expectedAssemblyName, assembly.GetName().Name);
    }
}