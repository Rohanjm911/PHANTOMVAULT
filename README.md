# PHANTOMVAULT

> **Trust Nothing. Reveal Nothing.**

PHANTOMVAULT is a high-security, privacy-first native Windows desktop application built for encrypted local file storage, undetectable file concealment via steganography, multi-vault compartmentalization, automated emergency lock policies, and resilient cryptographic backup & recovery.

---

## Key Highlights

- **Native Windows Desktop Experience**: Built with WinUI 3, Windows App SDK, and modern .NET 8 LTS.
- **Strong Cryptographic Primitives**: AES-256-GCM authenticated encryption, Argon2id key derivation with high memory hardness, and cryptographically secure random nonces.
- **Zero Remote Dependency**: Completely local-first, offline-capable, with zero telemetry, zero trackers, and no mandatory cloud accounts.
- **Chunked File Encryption**: 64 KiB independently authenticated chunks with chunk-index binding and unique nonces, preventing chunk reordering, truncation, or splicing attacks.
- **PNG & WAV Steganography**: Embed encrypted files into standard PNG images (and WAV audio) with format validation, capacity checking, containerized integrity tags, and non-destructive output generation.
- **Multiple Isolated Vaults**: Independent master keys, separate salts, and decoupled access credentials.
- **Emergency Lock & Dead-Man's Switch**: Instant global lock on workstation lock or configurable idle timeouts, with safe countdown policies.
- **Encrypted Backup & Recovery**: Cryptographically verified backup archives with version integrity enforcement.
- **Sanitized Audit Trail**: Local activity history strictly scrubbed of passwords, keys, and confidential path metadata.

---

## User Interface & Design System

PHANTOMVAULT delivers a minimalist, technical, high-contrast dark desktop interface inspired by advanced cybersecurity tooling. It avoids gradients, glassmorphism, glowing borders, or frivolous animations in favor of precision, technical credibility, and rapid keyboard navigation.

### Official Design Tokens
- **Primary Background**: `#0B0D12` (Deep Obsidian)
- **Secondary Background**: `#11151D` (Dark Surface)
- **Sidebar**: `#0E1118` (Enclave Slate)
- **Elevated Surface**: `#171C26` (Card Container)
- **Primary Accent**: `#39D0C8` (Cyber Teal)
- **Secondary Accent**: `#7C83FF` (Periwinkle Indigo)
- **Primary Text**: `#F3F5F9` (Pure Contrast)
- **Secondary Text**: `#A1A9B8` (Subdued Slate)
- **Muted Text**: `#697386` (Technical Labels)
- **Success / Valid**: `#36C98F` (Emerald Shield)
- **Warning**: `#F2B84B` (Amber Notice)
- **Error / Lockdown**: `#F06A75` (Crimson Alert)
- **Borders & Dividers**: `#252C38` (Precision 1px Outlines)

### Primary Workspace Views
1. **First-Run Experience**: Zero-telemetry welcome wizard introducing local storage guarantees, password responsibilities, and master key initialization.
2. **Dashboard (`DashboardView`)**: Live overview of total, locked, and unlocked vaults, ciphertext on disk, last backup date, quick actions, security posture status, and real security notices.
3. **My Vaults (`VaultsView`)**: Multi-vault compartmentalization management with independent master keys, status pills, creation wizard with entropy rating, and unlock modal.
4. **Vault Workspace (`VaultView`)**: Focused file manager with breadcrumbs (`PHANTOMVAULT > [Vault] > Encrypted Files`), search filter, storage stats, and inline import, export, rename, and permanent deletion controls.
5. **Steganography Studio (`StegoView`)**: Guided 6-step file concealment workflow into PNG scanlines or WAV PCM samples with real-time capacity validation, password entropy meters, and an extraction & integrity verification studio.
6. **Recovery & Backups (`BackupView`)**: Compressed `.pvbackup` packaging with cryptographic manifest verification, archive inspection, and restore-to-new-vault workflows.
7. **Security Center (`SecurityCenterView`)**: Live audit dashboard detailing algorithmic specifications (AES-256-GCM, Argon2id, 64 KiB framing), memory zeroization, storage data integrity audits, and instant emergency lockdown triggers.
8. **Emergency Access (`EmergencyAccessView`)**: Configurable session defense controls, inactivity timeouts, workstation lock listener (Win + L), and dead-man's switch countdown with test mode.
9. **Activity Log (`ActivityLogView`)**: Local audit trail with category filtering (Vault, File, Backup, Security, Errors), live search, and privacy-preserving log clearing.
10. **Settings (`SettingsView`)**: General directories, zero-telemetry declarations, backup reminders, and technical security limitations disclosure.

---

## Architecture Overview

```
PHANTOMVAULT/
├── PHANTOMVAULT.sln
├── src/
│   ├── PhantomVault.App/            # WinUI 3 Desktop Shell & MVVM Views
│   ├── PhantomVault.Core/           # Models, Interfaces, Enums, Exceptions
│   ├── PhantomVault.Security/       # AES-256-GCM, Argon2id, Key Management, Auto-Lock
│   ├── PhantomVault.Storage/        # SQLite Repositories, Atomic File Ops, Chunking
│   ├── PhantomVault.Steganography/  # PNG & WAV LSB Encoders, Decoders, Capacity Engine
│   └── PhantomVault.Infrastructure/ # Backup/Restore, Activity Logging, OS Integration
├── tests/
│   ├── PhantomVault.Core.Tests/
│   ├── PhantomVault.Security.Tests/
│   ├── PhantomVault.Storage.Tests/
│   ├── PhantomVault.Steganography.Tests/
│   └── PhantomVault.Integration.Tests/
└── docs/                           # Comprehensive security, threat model & crypto docs
```

---

## Quick Start (Building & Running)

### Prerequisites
- Windows 10 (version 1809+ / build 17763) or Windows 11
- .NET 8 SDK or higher
- Windows App SDK / WinUI 3 workload

### Build via Command Line
```powershell
dotnet build PHANTOMVAULT.sln -c Release
```

### Run Tests
```powershell
dotnet test PHANTOMVAULT.sln --logger "console;verbosity=normal"
```

### Run Application
```powershell
dotnet run --project src/PhantomVault.App/PhantomVault.App.csproj
```

---

## Documentation Index

- [Architecture & Boundaries](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/docs/ARCHITECTURE.md)
- [Threat Model](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/docs/THREAT_MODEL.md)
- [Security Model & Guarantees](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/docs/SECURITY_MODEL.md)
- [Cryptography Specification](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/docs/CRYPTOGRAPHY.md)
- [Vault Storage Format Specification](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/docs/VAULT_FORMAT.md)
- [Backup and Recovery Manual](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/docs/BACKUP_AND_RECOVERY.md)
- [Build and Installation Guide](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/docs/BUILD_AND_INSTALL.md)
- [Testing Verification Guide](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/docs/TESTING.md)

---

## License

Licensed under the MIT License. See LICENSE for details.
