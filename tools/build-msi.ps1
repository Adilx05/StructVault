param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoIncrement
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$versionFile = Join-Path $repoRoot 'Directory.Build.props'
$installerProject = Join-Path $repoRoot 'installer/StructVault.Installer/StructVault.Installer.wixproj'

[xml]$versionXml = Get-Content $versionFile
$propertyGroup = $versionXml.Project.PropertyGroup | Select-Object -First 1
$versionPatchNode = $propertyGroup.SelectSingleNode('VersionPatch')
$major = [int]$propertyGroup.VersionMajor
$minor = [int]$propertyGroup.VersionMinor
$patch = [int]$versionPatchNode.InnerText
$version = "$major.$minor.$patch"

Write-Host "Building StructVault MSI version $version..."
dotnet build $installerProject -c $Configuration -p:VersionPatch=$patch

if (-not $NoIncrement) {
    $nextPatch = $patch + 1
    if ($nextPatch -gt 65535) {
        throw 'MSI ProductVersion patch number cannot exceed 65535.'
    }

    $versionPatchNode.InnerText = [string]$nextPatch
    $versionXml.Save($versionFile)
    Write-Host "Next MSI build version prepared: $major.$minor.$nextPatch"
}
