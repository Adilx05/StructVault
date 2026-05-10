# StructVault

[![Release](https://img.shields.io/github/v/release/Adilx05/StructVault?include_prereleases&label=Release&color=2563eb)](https://github.com/Adilx05/StructVault/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/Adilx05/StructVault/total?label=Downloads&color=10b981)](https://github.com/Adilx05/StructVault/releases)
[![License](https://img.shields.io/github/license/Adilx05/StructVault?label=License&color=f59e0b)](LICENSE)
[![Last Commit](https://img.shields.io/github/last-commit/Adilx05/StructVault?label=Last+Commit&color=64748b)](https://github.com/Adilx05/StructVault/commits/main)

> **StructVault** — A fast, secure, offline-first structured vault system for Windows. Store sensitive data in encrypted vault files with a hierarchical structure.

## Features

- **Encrypted Storage** - Vault data protected with AES-256-GCM encryption and Argon2id key derivation
- **Hierarchical Structure** - Organize data in a tree of nodes, each containing named fields
- **Sensitive Field Masking** - Hide sensitive values by default; reveal via clipboard copy
- **Manual Save** - Explicit save with dirty tracking (no autosave)
- **Clipboard Auto-Clear** - Automatically clear copied values from clipboard after a configurable delay
- **Idle Lock** - Lock the vault after configurable inactivity timeout
- **Theme Support** - Multiple MahApps.Metro color themes
- **Drag & Drop** - Reorder nodes and fields within their parent via drag and drop
- **Search** - Find nodes and fields across the vault

## Architecture

Clean Architecture with five layers:

| Layer | Purpose |
|-------|---------|
| **Domain** | Core entities and shared types |
| **Application** | Use cases, commands, queries, abstractions |
| **Infrastructure** | Security services (Argon2id, AES-256-GCM), file I/O, logging |
| **Persistence** | SQLite database schema and operations |
| **Desktop** | WPF UI with MVVM pattern, MahApps.Metro controls |

## Technology Stack

- **.NET 8.0** (Windows)
- **WPF** with MahApps.Metro 2.4.11
- **SQLite** via Microsoft.Data.Sqlite
- **MediatR** for CQRS pattern
- **Argon2id** via Konscious.Security.Cryptography.Argon2
- **AES-256-GCM** via built-in .NET cryptography

## Vault File Format (.qps)

Vault files use a custom QPS format with the following structure:

1. **13-byte header** - Magic bytes ("QPSV"), version, salt/IV/tag sizes, ciphertext length
2. **Salt** - 16+ bytes for key derivation
3. **Initialization Vector** - 12 bytes for AES-GCM
4. **Ciphertext** - Encrypted SQLite database with 16-byte authentication tag

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- Windows 10/11

### Build

```powershell
dotnet build StructVault.sln
```

### Run

```powershell
dotnet run --project src/StructVault.Desktop/StructVault.Desktop.csproj
```

### Test

```powershell
dotnet test StructVault.sln
```

### Build MSI setup

```powershell
./tools/build-msi.ps1
```

The MSI setup is generated with WiX, shows a standard wizard with the MIT license
and install-directory selection, creates desktop and Start menu shortcuts,
registers `.qps` vault files with Windows, and makes double-clicked QPS files open
in StructVault from the selected install folder. The current product version
starts at `1.0.0`; the setup build script automatically increments the patch
number in `Directory.Build.props` after each successful installer build.

## Usage

1. **Open/Create Vault** - Use the file dialog to load a `.qps` vault file or create a new one
2. **Add Nodes** - Right-click the tree to add root or child nodes
3. **Add Fields** - Select a node, then add key-value fields to it
4. **Save** - Click the Save button to write changes to the vault file
5. **Lock** - The vault auto-locks after the configured idle timeout; enter your password to unlock

## Security Design

- **Key Derivation**: Argon2id with configurable memory/time cost
- **Encryption**: AES-256-GCM with random salt and IV per file
- **No Plaintext on Disk**: All sensitive data stays encrypted until unlocked
- **Password Required**: Vaults cannot be created or opened without a non-empty master password

## Project Structure

```
src/
  StructVault.Domain/         - Core domain types
  StructVault.Application/    - Commands, queries, service interfaces
  StructVault.Infrastructure/ - Crypto, file I/O, logging implementations
  StructVault.Persistence/    - SQLite schema and data access
  StructVault.Desktop/        - WPF application, ViewModels, Views
tests/
  StructVault.Architecture.Tests/ - Architecture and integration tests
```

## Download & Install

Download the latest release from the [Releases](https://github.com/Adilx05/StructVault/releases/latest) page.

Run the MSI installer to set up StructVault with Start Menu and Desktop shortcuts. Vault files (`.qps`) are automatically associated with StructVault after installation.

## License

StructVault is licensed under the MIT License. See [LICENSE](LICENSE) for details.
