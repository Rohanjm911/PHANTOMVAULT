# PHANTOMVAULT Vault Format Specification

## 1. Directory Structure

A PHANTOMVAULT vault directory (typically `<VaultName>.pvault`) has the following layout on disk:

```
<VaultName>.pvault/
├── vault.header          # Encrypted master key & KDF parameters
├── catalog.db            # SQLite database with encrypted file entries
├── data/                 # Encrypted chunked file payloads
│   ├── 4a8b...2f.pvc     # File UUID 1 payload
│   └── d91c...10.pvc     # File UUID 2 payload
└── .lock                 # Ephemeral lock token (prevent multi-instance write collision)
```

---

## 2. Vault Header Binary Format (`vault.header`)

The vault header file is 108 bytes fixed length:

| Field | Type / Size | Description |
| :--- | :--- | :--- |
| **Magic Bytes** | 4 bytes (`ASCII`) | `PVLT` (`0x50 0x56 0x4C 0x54`) |
| **Format Version** | uint16 (`0x0001`) | Major format version number |
| **KDF Identifier** | uint16 | `1` = Argon2id, `2` = PBKDF2-HMAC-SHA256 |
| **KDF Salt** | 16 bytes | Cryptographically random salt |
| **KDF Iterations** | uint32 | Iteration count (e.g., 4 for Argon2id, 600,000 for PBKDF2) |
| **KDF Memory (KiB)**| uint32 | Memory parameter (e.g., 65,536 for 64 MiB) |
| **KDF Parallelism** | uint32 | Thread/lane count (e.g., 4) |
| **Key Wrap Nonce** | 12 bytes | 96-bit AES-GCM nonce |
| **Wrapped Key Data**| 32 bytes | Ciphertext of 256-bit Master DEK |
| **Header Auth Tag** | 16 bytes | 128-bit AES-GCM authentication tag |
| **Header AAD** | Implicit | Bound to Magic + Version + KDF ID + Salt + Params |

Any tampering with any byte in `vault.header` results in an immediate GCM authentication failure upon password entry.

---

## 3. SQLite Metadata Catalog Schema (`catalog.db`)

The catalog contains tables tracking encrypted entries, directories, and policies:

```sql
-- Vault File Index (All sensitive metadata encrypted with DEK)
CREATE TABLE IF NOT EXISTS VaultEntries (
    Id TEXT PRIMARY KEY,               -- GUID / UUID string
    ParentId TEXT NULL,                -- Parent directory entry ID
    EncryptedName BLOB NOT NULL,       -- AES-256-GCM encrypted original filename
    EncryptedMetadata BLOB NOT NULL,   -- AES-256-GCM encrypted JSON with size, dates, mime
    FileSize INTEGER NOT NULL,         -- Ciphertext size on disk in bytes
    PlaintextSize INTEGER NOT NULL,    -- Original plaintext size in bytes
    Sha256Checksum TEXT NOT NULL,      -- Plaintext SHA-256 hex digest
    IsDirectory INTEGER NOT NULL,      -- 1 = folder, 0 = file
    CreatedAt INTEGER NOT NULL,        -- Unix epoch UTC
    ModifiedAt INTEGER NOT NULL        -- Unix epoch UTC
);

-- Vault Properties and Preferences
CREATE TABLE IF NOT EXISTS VaultProperties (
    Key TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);
```

---

## 4. Atomic File Operations

To prevent data corruption caused by power interruptions, crashes, or disk full conditions:
1. Writes are directed to `<UUID>.pvc.tmp` in the `data/` folder.
2. Complete file content is flushed with `FileStream.Flush(flushToDisk: true)`.
3. The temporary file is atomically renamed to `<UUID>.pvc`.
4. The database row is committed within an explicit SQLite transaction.
5. If any error occurs prior to commit, the `.tmp` file is immediately deleted, and the database transaction is rolled back.
