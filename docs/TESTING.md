# PHANTOMVAULT Automated Testing Guide

## 1. Test Architecture

The PHANTOMVAULT test suite uses **xUnit** and is divided into five test assemblies:

| Test Project | Focus Areas |
| :--- | :--- |
| **`PhantomVault.Core.Tests`** | Models, validation rules, path traversal defenses, password entropy. |
| **`PhantomVault.Security.Tests`** | AES-256-GCM encryption/decryption, Argon2id & PBKDF2 KDF verification, key wrapping, nonce uniqueness, anti-tampering verification, memory zeroization. |
| **`PhantomVault.Storage.Tests`** | Vault repository CRUD, SQLite metadata catalog transactions, chunked file manager atomic writes, corrupted blob handling. |
| **`PhantomVault.Steganography.Tests`** | PNG LSB carrier embedding & extraction, container magic byte validation, capacity boundary checks, oversized payload rejection, audio WAV steganography. |
| **`PhantomVault.Integration.Tests`** | End-to-end vault creation, lock/unlock cycles, file import/export round-trip, backup archive packaging and restoration. |

---

## 2. Executing the Test Suites

### Run All Tests via CLI
```powershell
dotnet test PHANTOMVAULT.sln --logger "console;verbosity=normal"
```

### Run a Specific Test Project
```powershell
dotnet test tests/PhantomVault.Security.Tests/PhantomVault.Security.Tests.csproj
dotnet test tests/PhantomVault.Steganography.Tests/PhantomVault.Steganography.Tests.csproj
dotnet test tests/PhantomVault.Storage.Tests/PhantomVault.Storage.Tests.csproj
```

### Filtering Specific Categories or Tests
```powershell
dotnet test PHANTOMVAULT.sln --filter "FullyQualifiedName~AesGcm"
dotnet test PHANTOMVAULT.sln --filter "FullyQualifiedName~Steganography"
```

---

## 3. Cryptographic Verification Matrix

The test suite validates the following negative security properties:
- [x] Incorrect password yields `CryptographicIntegrityException` and zero plaintext leakage.
- [x] Corrupted single bit in ciphertext throws `CryptographicIntegrityException`.
- [x] Corrupted AAD (e.g. modified chunk index or wrong file UUID) is rejected.
- [x] Truncated file chunk fails authentication.
- [x] Directory traversal in imported file (`../../sensitive.txt`) is detected and blocked.
- [x] Stego carrier capacity exhaustion throws `CarrierCapacityExceededException`.
- [x] Non-PNG / corrupt carrier files are rejected immediately.
