# PHANTOMVAULT Backup and Recovery Guide

## 1. Overview

PHANTOMVAULT provides a zero-leakage, encrypted backup mechanism. A backup package (`.pvbackup`) contains the encrypted vault header, encrypted metadata catalog, and all encrypted `.pvc` chunk payloads in a single compressed container, protected by an authenticated backup manifest.

---

## 2. Backup Archive Structure

A `.pvbackup` archive contains:

```
[backup_manifest.json]      # Signed manifest with vault ID, timestamps, file hashes
[vault.header]              # Original encrypted vault header (requires master password)
[catalog.db]                # Encrypted metadata catalog
[data/]                     # Encrypted file blobs
    [<UUID1>.pvc]
    [<UUID2>.pvc]
```

### Backup Manifest Example (`backup_manifest.json`)
```json
{
  "ManifestVersion": 1,
  "VaultId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "VaultName": "Confidential Research",
  "CreatedAtUtc": "2026-10-25T09:00:00Z",
  "AppVersion": "1.0.0",
  "FileCount": 42,
  "TotalCiphertextBytes": 104857600,
  "HeaderSha256": "3a7bd3e2360a3d29eea436fcfb7e44c735d117c42d1c1835420b6b9942dd4f1b",
  "CatalogSha256": "8f434346648f6b96df89dda901c5176b10a6d83961dd3c1ac88b59b2dc327aa4",
  "ArchiveChecksum": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
}
```

---

## 3. Creating a Backup

1. Open PHANTOMVAULT and unlock the target vault (or select an existing vault in the dashboard).
2. Navigate to **Backup & Recovery**.
3. Select **Create Encrypted Backup**.
4. Choose destination path (e.g. `D:\Backups\Vault_20261025.pvbackup` or external USB drive).
5. The application computes SHA-256 hashes of all components, generates the manifest, and writes the `.pvbackup` package atomically.
6. The backup verification phase automatically runs immediately after packaging to confirm archive integrity.

---

## 4. Restoring from a Backup

Restoration is designed to prevent accidental data overwrites:

1. In the **Backup & Recovery** view, select **Restore Backup**.
2. Select the target `.pvbackup` file.
3. PHANTOMVAULT verifies:
   - Archive integrity and zip structure.
   - `backup_manifest.json` schema and compatibility version.
   - SHA-256 checksums of the included header, catalog, and file blobs against the manifest.
4. If corruption or mismatch is detected, restoration is rejected with detailed error diagnostics.
5. If valid, the user chooses the target directory:
   - **Recommended**: Restore into a new vault directory to avoid modifying existing files.
   - **Overwrite**: If targeting an existing vault, explicit confirmation is requested.
6. The user enters the original vault master password to verify unlockability.

---

## 5. Credential Loss & Consequences

> [!CAUTION]
> **Trust Nothing. Reveal Nothing.**
> PHANTOMVAULT has no backdoor, no cloud recovery, and no hard-coded master passwords. If you lose your master password, your encrypted data cannot be decrypted by anyone, including the software authors. Keep safe offline backups of your master password and emergency recovery phrases.
