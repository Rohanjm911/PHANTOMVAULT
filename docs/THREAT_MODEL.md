# PHANTOMVAULT Threat Model

## 1. Executive Summary

PHANTOMVAULT is designed to protect personal confidential files stored locally on Windows workstations against unauthorized access, offline password guessing, storage media theft, forensic image recovery, and accidental exposure. This document outlines the assets, adversary tiers, threat vectors, mitigations, and explicit residual risks.

---

## 2. Protected Assets

| Asset | Description | Security Objective |
| :--- | :--- | :--- |
| **Vault Files (Payloads)** | User confidential files, images, credentials, notes | Confidentiality, Authenticity, Integrity |
| **File Metadata** | Filenames, directory hierarchy, original timestamps, file sizes | Confidentiality, Integrity (Encrypted at rest) |
| **Cryptographic Keys** | Data Encryption Keys (DEKs), derived Key Encryption Keys (KEKs) | Confidentiality, Zero Plaintext Persistence |
| **Steganographic Payloads** | Hidden files embedded inside carrier images/audio | Confidentiality, Deniability, Integrity |
| **Backup Archives** | Offline `.pvbackup` packages | Authenticity, Versioned Integrity, Confidentiality |
| **Audit Trails** | Local operation logs | Integrity, Zero Secret Leakage (Sanitized) |

---

## 3. Adversary Capabilities & Threat Actors

### Tier 1: Opportunistic Physical Access (Stolen Device / Device Left Unattended)
- Attacker boots device from external drive or steals laptop.
- Directly accesses raw NTFS/ReFS filesystem or reads disk images.
- **Defense**: All vault files, metadata databases, and backups are encrypted at rest with AES-256-GCM. Strong Argon2id KDF prevents fast brute-force dictionary attacks.

### Tier 2: Malware Operating Under Normal User Context
- Keyloggers, screen grabbers, or memory scrapers running under the same user session.
- **Defense & Boundary**: While the vault is locked, ciphertext remains secure. When unlocked, keys reside in volatile memory. PHANTOMVAULT employs `SecureBuffer`, pinned memory, zero-filling (`CryptographicOperations.ZeroMemory`) upon locking, and minimal plaintext exposure windows. However, an attacker with code execution in the user's session can read user space memory. PHANTOMVAULT clearly documents this limitation: OS-level kernel isolation or elevated compromises are out of scope.

### Tier 3: Forensic & Statistical Analysis
- Forensic investigators running file carving, timeline analysis, and steganographic detection (chi-square tests, sample pair analysis).
- **Defense**: PNG steganography utilizes an authenticated container format (`PVST`) with random initialization vectors and pseudo-random bit dispersion. PHANTOMVAULT **explicitly disclaims** mathematically undetectable steganography: carrier modifications alter statistical distributions, so users are advised against relying solely on plausible deniability under advanced laboratory forensic scrutiny.

---

## 4. Threat Matrix & Mitigations

| Threat | Attack Vector | Mitigation Strategy |
| :--- | :--- | :--- |
| **Offline Dictionary Attack** | Attacker dumps vault header and tests passwords offline | Argon2id KDF (memory: 64MB, iterations: 4, parallelism: 4) or PBKDF2 (600,000 rounds). Makes GPU/ASIC attacks cost-prohibitive. |
| **Ciphertext Tampering / Splicing** | Attacker modifies encrypted file bytes or swaps chunks between files | AES-256-GCM authenticated encryption. Each 64 KiB chunk binds File UUID, Chunk Index, and Last-Chunk indicator into AAD. Any mutation triggers immediate authentication failure and halts read. |
| **Metadata Leakage** | Plaintext filenames revealed in filesystem directory structure | File storage uses UUID-named encrypted blobs (`<UUID>.pvc`). Directory structure and filenames are encrypted inside the vault's SQLite metadata catalog (`vault.db`). |
| **Nonce Reuse Attack** | Encrypting multiple chunks with same AES-GCM nonce | Nonce derivation combines a cryptographically random 64-bit IV prefix per file with a monotonically increasing 32-bit chunk counter, mathematically guaranteeing zero nonce collisions under the DEK. |
| **Accidental Data Loss** | Power outage or system crash during import/backup write | Atomic file operations: write to `.tmp` file, flush to disk, verify authentication tag, then atomic filesystem rename. SQLite transactions ensure ACID metadata consistency. |
| **Path Traversal / Zip Slip** | Malicious file entry names containing `../../` | Strict path sanitization on import/export: reject absolute paths, resolve relative paths against designated vault root, strip directory traversal tokens. |
| **Sensitive Data in Logs** | Password or key inadvertently written to disk logs | Dedicated log sanitizer filter strips all secrets, key bytes, passwords, and sensitive path tokens before formatting. |
| **Workstation Abandonment** | User leaves machine unlocked in a public area | Emergency lock policies: inactivity timer auto-lock, Windows Session Lock listener, and configurable countdown warning. |

---

## 5. Explicit Security Limitations & Residual Risks

1. **Volatile Memory Scrapers**: If an endpoint is already compromised by rootkits or user-level spyware with debugging privileges, keys in volatile memory during active vault usage can theoretically be extracted.
2. **Crash Dumps & Paging File**: Windows may page active memory to `pagefile.sys` or write crash dumps during kernel panics. PHANTOMVAULT explicitly recommends enabling Windows BitLocker full-disk encryption to protect swap and dump artifacts.
3. **Plausible Deniability**: Windows NTFS keeps journal records ($UsnJrnl, $Mft), shellbags, and jump lists of accessed file paths. PHANTOMVAULT cannot erase OS-level filesystem journals without administrator privileges.
4. **SSD Trim and Wear Leveling**: Secure file deletion cannot guarantee complete magnetic or flash block overwrite due to flash controller wear-leveling algorithms.
