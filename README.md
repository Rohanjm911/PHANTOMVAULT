# PHANTOMVAULT

<div align="center">

> **Trust Nothing. Reveal Nothing.**

**A high-security, privacy-first native Windows desktop application for authenticated local file encryption, covert steganography, multi-vault isolation, and automated emergency lock policies.**

[![Build](https://img.shields.io/badge/Build-Passing-36C98F?style=for-the-badge&logo=githubactions&logoColor=white)](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/README.md)
[![Tests](https://img.shields.io/badge/Tests-49%20Passed%20(100%25)-36C98F?style=for-the-badge&logo=checkmarx&logoColor=white)](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/docs/TESTING.md)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D4?style=for-the-badge&logo=windows11&logoColor=white)](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/README.md)
[![Framework](https://img.shields.io/badge/.NET-8.0%20LTS-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/README.md)
[![UI](https://img.shields.io/badge/WinUI-3%20%2F%20Windows%20App%20SDK-0078D4?style=for-the-badge&logo=windows&logoColor=white)](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/README.md)
[![Crypto](https://img.shields.io/badge/AES--256--GCM-Authenticated-39D0C8?style=for-the-badge&logo=authentik&logoColor=black)](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/docs/CRYPTOGRAPHY.md)
[![KDF](https://img.shields.io/badge/Argon2id-Memory%20Hard-7C83FF?style=for-the-badge&logo=1password&logoColor=white)](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/docs/CRYPTOGRAPHY.md)
[![License](https://img.shields.io/badge/License-MIT-F2B84B?style=for-the-badge)](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/README.md)

<br/>

<p align="center">
  <img src="https://skillicons.dev/icons?i=cs,dotnet,windows,sqlite,git,visualstudio" alt="PHANTOMVAULT Core Tech Stack" />
</p>

</div>

---

## 🛠️ Technology Stack & Frameworks

PHANTOMVAULT is built using enterprise-grade engineering technologies with strict adherence to native Windows UX patterns, zero-telemetry offline storage, and modern cryptographic standards:

| Real-Life Logo | Technology | Version / Standard | Purpose in PHANTOMVAULT |
| :---: | :--- | :--- | :--- |
| <img src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/csharp/csharp-original.svg" width="44" height="44" alt="C#" /> | **C# 12** | `12.0` | Core application logic, memory-pinned buffers (`Span<byte>`, `Memory<byte>`), safe cryptographic zeroization, and MVVM business services. |
| <img src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/dotnetcore/dotnetcore-original.svg" width="44" height="44" alt=".NET 8" /> | **.NET 8 LTS** | `8.0` | High-throughput runtime environment, asynchronous multi-threaded file chunking, and dependency injection container. |
| <img src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/windows11/windows11-original.svg" width="44" height="44" alt="WinUI 3" /> | **WinUI 3 & Windows App SDK** | `1.6+` | Native desktop user interface, custom cyber dark design token system, hardware-accelerated rendering, and fluent system shell. |
| <img src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/sqlite/sqlite-original.svg" width="44" height="44" alt="SQLite" /> | **SQLite 3** | `3.45+` | Local encrypted metadata indexing, transactional registry journaling, atomic rollback, and isolated per-vault catalog storage. |
| <img src="https://raw.githubusercontent.com/FortAwesome/Font-Awesome/6.x/svgs/solid/shield-halved.svg" width="44" height="44" alt="AES-256-GCM" /> | **AES-256-GCM** | `NIST SP 800-38D` | Authenticated symmetric encryption with 64 KiB independently verified chunks, 128-bit authentication tags, and unique nonces per chunk. |
| <img src="https://raw.githubusercontent.com/FortAwesome/Font-Awesome/6.x/svgs/solid/lock.svg" width="44" height="44" alt="Argon2id" /> | **Argon2id KDF** | `RFC 9106` | Memory-hard password key derivation (64 MiB RAM, 4 iterations, 4 parallelism lanes) to resist GPU/ASIC brute-force attacks. |
| <img src="https://raw.githubusercontent.com/devicons/devicon@latest/icons/unifiedmodelinglanguage/unifiedmodelinglanguage-original.svg" width="44" height="44" alt="Steganography" /> | **PNG & WAV Steganography** | `LSB v2.0` | 6-step covert carrier engine embedding encrypted payloads into PNG RGBA scanlines and WAV 16-bit PCM audio samples with zero perceptual distortion. |
| <img src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/visualstudio/visualstudio-original.svg" width="44" height="44" alt="Visual Studio" /> | **Visual Studio / MSBuild** | `2022 / v17` | Multi-target compilation, unpackaged desktop tooling, and Windows App SDK build management. |
| <img src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/git/git-original.svg" width="44" height="44" alt="Git" /> | **Git & GitHub** | `2.40+` | Modular version control with isolated feature branches, reproducible build manifests, and strict commit provenance. |
| <img src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/markdown/markdown-original.svg" width="44" height="44" alt="Documentation" /> | **Markdown & Threat Specs** | `GFM` | In-depth threat models, cryptographic bounds, STRIDE analysis, and formal verification specifications. |

---

## 🛡️ Key Security Features

- **Chunked Authenticated Encryption**: 64 KiB independently authenticated chunks with chunk-index binding and unique nonces, preventing chunk reordering, truncation, or splicing attacks.
- **Argon2id Key Derivation**: High memory hardness (64 MiB, 4 iterations, 4 lanes) ensures offline master password attacks are computationally prohibitive.
- **Dual-Carrier Steganography**: Embed encrypted files into standard PNG images (scanline RGBA) or WAV audio (16-bit PCM samples) with capacity verification and integrity checks.
- **Multi-Vault Compartmentalization**: Independent master keys, separate salts, and decoupled access credentials ensure breach containment.
- **Zero Remote Dependency**: Completely local-first, offline-capable, with zero telemetry, zero analytics, zero external network sockets, and no mandatory cloud accounts.
- **Automated Emergency Locks**:
  - Instant global lockdown on Windows workstation lock (`Win + L`).
  - Configurable inactivity auto-lock.
  - Dead-Man's switch countdown with test mode and immediate zeroization.
- **Memory Zeroization**: Plaintext master keys, derived keys, and unencrypted file buffers are scrubbed from memory immediately after use via `CryptographicOperations.ZeroMemory()`.
- **Encrypted Backup & Recovery**: Cryptographically verified `.pvbackup` packages with version integrity enforcement.
- **Sanitized Audit Trail**: Local activity history strictly scrubbed of passwords, keys, and confidential path metadata.

---

## 🎨 User Interface & Design Tokens

PHANTOMVAULT delivers a minimalist, technical, high-contrast dark desktop interface inspired by advanced cybersecurity tooling. It avoids generic colors, frivolous animations, and bright distractions in favor of precision and technical credibility.

### Official Cyber Dark Tokens
```
Primary Background    : #0B0D12 (Deep Obsidian)
Secondary Background  : #11151D (Dark Surface)
Sidebar / Navigation  : #0E1118 (Enclave Slate)
Elevated Surface      : #171C26 (Card Container)
Primary Accent        : #39D0C8 (Cyber Teal)
Secondary Accent      : #7C83FF (Periwinkle Indigo)
Primary Text          : #F3F5F9 (Pure Contrast)
Secondary Text        : #A1A9B8 (Subdued Slate)
Muted Text            : #697386 (Technical Labels)
Success / Valid       : #36C98F (Emerald Shield)
Warning               : #F2B84B (Amber Notice)
Error / Lockdown      : #F06A75 (Crimson Alert)
Borders & Dividers    : #252C38 (Precision 1px Outlines)
```

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

## 🚀 Quick Start (Running & Building)

### Option 1: Direct Standalone Launch (Root Folder)
The app is pre-built as a self-contained release in the root directory. You can run it immediately without compiling:

1. **Native Launcher**: Double-click [`PhantomVault.exe`](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/PhantomVault.exe) in the root folder.
2. **Desktop Shortcut**: Double-click [`PhantomVault.lnk`](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/PhantomVault.lnk).
3. **Batch Launcher**: Double-click [`Launch-PHANTOMVAULT.bat`](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/Launch-PHANTOMVAULT.bat).
4. **Direct Binaries**: Run [`publish/PhantomVault.App.exe`](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/publish/PhantomVault.App.exe).

---

### Option 2: Build & Run from Source (Developer CLI)

#### Prerequisites
- Windows 10 (version 1809+ / build 17763) or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or higher

#### 1. Compile the Solution
```powershell
dotnet build PHANTOMVAULT.sln -c Release
```

#### 2. Execute Automated Tests (49 Passing)
```powershell
dotnet test PHANTOMVAULT.sln --logger "console;verbosity=normal"
```

#### 3. Run the App
```powershell
dotnet run --project src/PhantomVault.App/PhantomVault.App.csproj
```

#### 4. Publish Standalone Release Package
```powershell
dotnet publish src/PhantomVault.App/PhantomVault.App.csproj -c Release -r win-x64 -o "publish"
```

---

## 📂 Repository Structure

```
PHANTOMVAULT/
├── PhantomVault.exe                 # Root standalone executable launcher
├── PhantomVault.lnk                 # Windows desktop shortcut with cyber shield icon
├── Launch-PHANTOMVAULT.bat          # Root batch launcher script
├── publish/                         # Standalone self-contained release package
│   ├── PhantomVault.App.exe         # WinUI 3 main binary
│   ├── Assets/                      # Application icons, branding & splash screens
│   └── *.dll                        # Runtime dependencies (WinUI 3, SQLite, DirectML, Argon2)
├── PHANTOMVAULT.sln                 # Visual Studio Solution
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

## 📚 Complete Technical Documentation

Comprehensive architectural, cryptographic, and operational manuals are available in the [`docs/`](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/docs/) directory:

- [Architecture & Module Boundaries](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/docs/ARCHITECTURE.md)
- [Threat Model & STRIDE Analysis](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/docs/THREAT_MODEL.md)
- [Security Model & Guarantees](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/docs/SECURITY_MODEL.md)
- [Cryptography Specification](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/docs/CRYPTOGRAPHY.md)
- [Vault Storage Format Specification](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/docs/VAULT_FORMAT.md)
- [Backup and Recovery Manual](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/docs/BACKUP_AND_RECOVERY.md)
- [Build and Installation Guide](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/docs/BUILD_AND_INSTALL.md)
- [Testing Verification Guide](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/docs/TESTING.md)

---

## 📄 License

Licensed under the [MIT License](file:///d:/projects%20and%20certificates/projects/apps/PHANTOMVAULT/README.md).
