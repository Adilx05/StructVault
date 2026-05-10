# StructVault MSI installer

StructVault uses WiX Toolset SDK to build a per-machine MSI setup package.
The installer publishes the WPF desktop app as a self-contained `win-x64` build,
packages the published output, and registers the `.qps` file type so double-clicking
a QPS vault opens StructVault.

## Build the MSI

```powershell
./tools/build-msi.ps1
```

The current product version starts at `1.0.0` in `Directory.Build.props`. The build
script passes that version into the WiX project and, after a successful MSI build,
automatically increments the patch number for the next setup build. Use
`-NoIncrement` when you need a reproducible local rebuild of the same version.

The generated MSI is written under:

```text
installer/StructVault.Installer/bin/<Configuration>/StructVault-Setup-<version>.msi
```
