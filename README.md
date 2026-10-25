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
