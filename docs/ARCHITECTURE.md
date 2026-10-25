# PHANTOMVAULT Architecture & Boundaries

## 1. Architectural Principles

PHANTOMVAULT follows a strict separation-of-concerns model based on **Hexagonal / Clean Architecture** and **MVVM** principles:
- **Decoupled Security Core**: The cryptographic and key management engines have zero dependencies on WinUI, XAML, or UI frameworks.
- **Dependency Inversion**: High-level application logic depends on abstractions (`IVaultRepository`, `ICryptographyService`, `ISteganographyService`, `IBackupService`), registered via `Microsoft.Extensions.DependencyInjection`.
- **Fault-Isolating Storage**: File operations utilize atomic scratch buffers and SQLite transactions, ensuring zero corruption during interrupted operations.
- **Strict Memory Hygeine**: Cryptographic keys are maintained in dedicated pinned/disposable wrappers and zeroized upon locking.

---

## 2. Solution Structure & Module Responsibilities

```
PHANTOMVAULT/
│
├── src/
│   ├── PhantomVault.Core/
│   │   ├── Models/               # Vault, FileEntry, BackupManifest, StegoPayload
│   │   ├── Interfaces/           # ICryptographyService, IVaultRepository, ISteganographyService
│   │   ├── DTOs/                 # View-independent data contracts
│   │   ├── Enums/                # VaultState, KdfAlgorithm, SecurityPolicyAction
│   │   ├── Validation/           # Path, password strength, capacity validators
│   │   └── Exceptions/           # Custom cryptographic & vault exceptions
│   │
│   ├── PhantomVault.Security/
│   │   ├── Cryptography/         # AES-256-GCM authenticated cipher engine
│   │   ├── KeyManagement/        # Argon2id / PBKDF2 KDF & Key Wrapping
│   │   ├── PasswordProtection/   # Complexity policies & entropy evaluation
│   │   ├── SecureBuffering/      # SecureBuffer & memory zeroization
│   │   └── SecurityPolicies/     # Emergency lock & inactivity coordinator
│   │
│   ├── PhantomVault.Storage/
│   │   ├── VaultRepository.cs    # Multi-vault lifecycle and persistence
│   │   ├── VaultFileManager.cs   # Chunked authenticated file read/write
│   │   ├── MetadataRepository.cs # SQLite-backed metadata storage
│   │   ├── DatabaseInitializer.cs# Schema migrations & table setup
│   │   └── StoragePaths.cs       # Path validation & traversal prevention
│   │
│   ├── PhantomVault.Steganography/
│   │   ├── ImageEncoder.cs       # PNG carrier LSB bit-embedding engine
│   │   ├── ImageDecoder.cs       # PNG carrier extraction & authentication
│   │   ├── AudioEncoder.cs       # WAV PCM audio carrier steganography
│   │   ├── CapacityCalculator.cs # Byte capacity & boundary checks
│   │   └── PayloadContainer.cs   # PVST stego container specification
│   │
│   ├── PhantomVault.Infrastructure/
│   │   ├── Logging/              # Sanitized activity logger & file sink
│   │   ├── WindowsIntegration/   # Workstation lock & power broadcast listeners
│   │   ├── Backup/               # .pvbackup packaging & archive creation
│   │   └── Recovery/             # Manifest verification & restoration
│   │
│   └── PhantomVault.App/
│       ├── App.xaml / App.xaml.cs # WinUI 3 App entry point & DI setup
│       ├── MainWindow.xaml       # Shell with NavigationView
│       ├── Views/                # Dashboard, Vault, Stego, Backup, Security, Settings
│       ├── ViewModels/           # CommunityToolkit.Mvvm ObservableObjects
│       ├── Controls/             # Custom security badges, progress meters
│       └── Converters/           # XAML value converters
│
└── tests/                        # 5 targeted xUnit test suites
```

---

## 3. Data Flow Diagrams

### Vault Unlock & Decryption Flow
```
[User Password]
      │
      ▼
[Argon2id KDF Engine]  <---  [Vault Salt + KDF Parameters from Vault Header]
      │
      ▼
[Key Encryption Key (KEK)]
      │
      ▼
[AES-256-GCM Unwrap]   <---  [Wrapped Master DEK + Header Auth Tag]
      │
      ▼
[Decrypted Master DEK] (Kept in SecureBuffer in RAM)
      │
      ▼
[Open SQLite Catalog]  <---  Decrypts metadata & file index
      │
      ▼
[Vault State = UNLOCKED]
```

### File Import & Chunked Encryption Flow
```
[User File on Disk]
      │
      ▼
[Path Traversal & Size Validation]
      │
      ▼
[Generate 128-bit File UUID + 64-bit Random IV Prefix]
      │
      ▼
[Stream in 64 KiB Chunks]
      │
      ▼
[For Each Chunk: AES-256-GCM]
   ├── Nonce: IV Prefix (8 bytes) + Chunk Index (4 bytes)
   ├── AAD: File UUID + Chunk Index + IsLastChunk
   └── Ciphertext + 16-byte Tag
      │
      ▼
[Write Atomic .tmp Chunked Blob -> Commit to <UUID>.pvc]
      │
      ▼
[Insert Encrypted Metadata Record into SQLite (in Transaction)]
```

---

## 4. Error Handling Strategy

1. **Security Failures**: Any authentication failure, tag mismatch, or corrupted header throws a typed `CryptographicIntegrityException` and halts immediately. Partial or unauthenticated data is never released or written to disk.
2. **Safe Rollback**: If an operation fails during file import or backup restore, the temporary files are securely deleted, the database transaction is rolled back, and the vault state remains intact.
3. **UI Isolation**: The UI observes changes via MVVM data bindings. Exceptions from background threads are caught by service coordinators, logged (with sensitive info scrubbed), and displayed as user-friendly InfoBars without crashing the application.
