# PHANTOMVAULT Cryptography Specification

## 1. Cryptographic Algorithms & Standards

PHANTOMVAULT adheres strictly to modern, NIST-approved and RFC-standardized cryptographic algorithms:

| Purpose | Algorithm / Primitive | Key / Parameter Size | Standard |
| :--- | :--- | :--- | :--- |
| **Symmetric Encryption** | AES-256-GCM | 256-bit key, 96-bit nonce, 128-bit tag | NIST SP 800-38D |
| **Password Key Derivation (Primary)** | Argon2id | 32-byte output, 16-byte salt, 64 MB memory, 4 iterations, 4 lanes | RFC 9106 |
| **Password Key Derivation (Fallback)** | PBKDF2-HMAC-SHA256 | 32-byte output, 16-byte salt, 600,000 iterations | RFC 8018 / NIST SP 800-132 |
| **Random Number Generation** | `System.Security.Cryptography.RandomNumberGenerator` | Hardware/OS entropy pool (BCryptGenRandom) | FIPS 140-2 compliant |
| **Memory Sanitization** | `CryptographicOperations.ZeroMemory` | Constant-time volatile memory wiping | .NET Core Security API |

---

## 2. Key Hierarchy & Lifecycle

```
[User Password (Secret)]
         │
         │  Argon2id (Salt: 16 bytes, Memory: 64MB, Iterations: 4)
         ▼
[Key Encryption Key (KEK) - 256 bits]
         │
         │  AES-256-GCM Wrap (Nonce: 12 bytes, Tag: 16 bytes)
         ▼
[Wrapped Master Key (Ciphertext: 32 bytes + Tag: 16 bytes)]
         ▲
         │ (Decryption via KEK)
         ▼
[Data Encryption Key (DEK) - 256 bits] (Randomly generated via CSPRNG)
         │
         ├── Encrypts File Chunks (AES-256-GCM with per-chunk Nonces & AAD)
         ├── Encrypts SQLite Metadata Catalog (`vault.db`)
         └── Wraps Stego Payloads and Backups
```

---

## 3. Nonce Management Strategy

AES-GCM catastrophic failure occurs if the same `(Key, Nonce)` pair is reused. To eliminate this risk:

1. **Vault Header Nonce**:
   - A single 12-byte random nonce is generated per key-wrapping operation. A new random nonce is drawn whenever the password changes.
2. **Chunked File Nonce Construction (96 bits / 12 bytes)**:
   ```
   [ 0..7  (8 bytes / 64 bits) ]: Cryptographically random File IV Prefix
   [ 8..11 (4 bytes / 32 bits) ]: Monotonically increasing Chunk Counter (Big-Endian uint32)
   ```
   - For any single file, the 32-bit counter supports files up to 256 TB ($2^{32} \times 64\text{ KiB}$).
   - Across different files, the 64-bit random IV prefix ensures negligible probability ($< 10^{-19}$) of collision under the same DEK.
3. **Associated Authenticated Data (AAD)**:
   Every chunk authenticates the following header in AAD:
   ```
   [ 0..15  (16 bytes) ]: File Unique Identifier (UUID / GUID)
   [ 16..19 (4 bytes)  ]: Chunk Index (Big-Endian uint32)
   [ 20     (1 byte)   ]: IsLastChunk (0x00 = Intermediate, 0x01 = Final)
   ```
   This mathematically binds each chunk to its parent file, prevents chunk reordering or dropping, and prevents truncating files.

---

## 4. Chunked Encryption Format (`.pvc`)

Every encrypted file on disk is stored in chunks:

```
+--------------------------------------------------------------------------+
| PVC Header: Magic "PVCF" (4B) | Version (2B) | Reserved (2B)             |
+--------------------------------------------------------------------------+
| Chunk 0:                                                                 |
|   - Nonce (12B)                                                          |
|   - Ciphertext Length (4B)                                               |
|   - Ciphertext (up to 65,536B)                                           |
|   - GCM Auth Tag (16B)                                                   |
+--------------------------------------------------------------------------+
| Chunk 1:                                                                 |
|   ...                                                                    |
+--------------------------------------------------------------------------+
| Chunk N (IsLast = 0x01):                                                 |
|   ...                                                                    |
+--------------------------------------------------------------------------+
```

---

## 5. Steganographic Container (`PVST`)

The container embedded in PNG/WAV carrier files has the following layout:

```
+--------------------------------------------------------------------------+
| Magic Bytes: 'P' 'V' 'S' 'T' (0x50 0x56 0x53 0x54)                       |
+--------------------------------------------------------------------------+
| Container Version: 0x0001 (uint16)                                       |
+--------------------------------------------------------------------------+
| KDF Algorithm: 0x01 (Argon2id) or 0x02 (PBKDF2) (uint8)                 |
+--------------------------------------------------------------------------+
| KDF Salt: 16 bytes                                                       |
+--------------------------------------------------------------------------+
| KDF Iterations: uint32                                                   |
+--------------------------------------------------------------------------+
| AES-GCM Nonce: 12 bytes                                                  |
+--------------------------------------------------------------------------+
| Encrypted Payload Length: uint32                                         |
+--------------------------------------------------------------------------+
| Encrypted Payload Data: N bytes                                          |
+--------------------------------------------------------------------------+
| AES-GCM Auth Tag: 16 bytes                                               |
+--------------------------------------------------------------------------+
```
Decrypted payload payload format:
- Original Filename Length (uint16)
- Original Filename (UTF-8 bytes)
- Original Content (bytes)
- SHA-256 Digest of Original Content (32 bytes) for post-decryption integrity verification.
