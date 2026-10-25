using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PhantomVault.Core.Exceptions;
using PhantomVault.Core.Interfaces;
using PhantomVault.Core.Models;
using PhantomVault.Core.Validation;

namespace PhantomVault.Infrastructure.Backup;

/// <summary>
/// Encrypted backup packaging, manifest verification, and disaster recovery service.
/// </summary>
public sealed class BackupService : IBackupService
{
    private readonly IVaultRepository _vaultRepo;

    public BackupService(IVaultRepository vaultRepo)
    {
        _vaultRepo = vaultRepo ?? throw new ArgumentNullException(nameof(vaultRepo));
    }

    public async Task<string> CreateBackupAsync(
        Guid vaultId, 
        string destinationArchivePath, 
        IProgress<double>? progress = null, 
        CancellationToken ct = default)
    {
        var vaults = await _vaultRepo.GetVaultsAsync().ConfigureAwait(false);
        var vault = Array.Find(vaults.ToArray(), v => v.Id == vaultId)
            ?? throw new PhantomVaultException("Vault not found.");

        string vaultDir = vault.VaultPath;
        if (!Directory.Exists(vaultDir))
            throw new DirectoryNotFoundException($"Vault folder does not exist: {vaultDir}");

        string destDir = Path.GetDirectoryName(destinationArchivePath) ?? string.Empty;
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        string tempArchivePath = destinationArchivePath + ".tmp";
        if (File.Exists(tempArchivePath))
        {
            File.Delete(tempArchivePath);
        }

        progress?.Report(0.1);

        try
        {
            var manifest = new BackupManifest
            {
                VaultId = vault.Id,
                VaultName = vault.Name,
                CreatedAtUtc = DateTime.UtcNow,
                AppVersion = "1.0.0",
                FileChecksums = new Dictionary<string, string>()
            };

            // Calculate header hash
            string headerPath = Path.Combine(vaultDir, "vault.header");
            if (File.Exists(headerPath))
            {
                manifest.HeaderSha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(headerPath, ct).ConfigureAwait(false)));
            }

            // Flush SQLite connection pools
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            // Calculate catalog hash with FileShare.ReadWrite
            string catalogPath = Path.Combine(vaultDir, "catalog.db");
            if (File.Exists(catalogPath))
            {
                using var fs = new FileStream(catalogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                manifest.CatalogSha256 = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct).ConfigureAwait(false));
            }

            // Calculate data files hashes
            string dataDir = Path.Combine(vaultDir, "data");
            long totalCipherBytes = 0;
            int fileCount = 0;

            if (Directory.Exists(dataDir))
            {
                foreach (string file in Directory.GetFiles(dataDir, "*.pvc"))
                {
                    ct.ThrowIfCancellationRequested();
                    string relativeName = "data/" + Path.GetFileName(file);
                    byte[] bytes = await File.ReadAllBytesAsync(file, ct).ConfigureAwait(false);
                    manifest.FileChecksums[relativeName] = Convert.ToHexString(SHA256.HashData(bytes));
                    totalCipherBytes += bytes.Length;
                    fileCount++;
                }
            }

            manifest.TotalCiphertextBytes = totalCipherBytes;
            manifest.FileCount = fileCount;

            progress?.Report(0.4);

            // Package into zip archive
            using (var zipStream = new FileStream(tempArchivePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false))
            {
                // 1. Add manifest
                var manifestEntry = archive.CreateEntry("backup_manifest.json", CompressionLevel.Optimal);
                using (var ms = manifestEntry.Open())
                {
                    await JsonSerializer.SerializeAsync(ms, manifest, new JsonSerializerOptions { WriteIndented = true }, ct).ConfigureAwait(false);
                }

                // 2. Add header
                if (File.Exists(headerPath))
                {
                    archive.CreateEntryFromFile(headerPath, "vault.header", CompressionLevel.NoCompression);
                }

                // 3. Add catalog using FileShare.ReadWrite stream
                if (File.Exists(catalogPath))
                {
                    var catEntry = archive.CreateEntry("catalog.db", CompressionLevel.Optimal);
                    using var catSrc = new FileStream(catalogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var catDst = catEntry.Open();
                    await catSrc.CopyToAsync(catDst, ct).ConfigureAwait(false);
                }

                // 4. Add encrypted data blobs
                if (Directory.Exists(dataDir))
                {
                    foreach (string file in Directory.GetFiles(dataDir, "*.pvc"))
                    {
                        ct.ThrowIfCancellationRequested();
                        archive.CreateEntryFromFile(file, "data/" + Path.GetFileName(file), CompressionLevel.NoCompression);
                    }
                }
            }

            progress?.Report(0.8);

            // Atomic rename
            if (File.Exists(destinationArchivePath))
            {
                File.Delete(destinationArchivePath);
            }
            File.Move(tempArchivePath, destinationArchivePath);

            progress?.Report(1.0);
            return destinationArchivePath;
        }
        catch
        {
            if (File.Exists(tempArchivePath))
            {
                try { File.Delete(tempArchivePath); } catch { }
            }
            throw;
        }
    }

    public async Task<BackupManifest> ValidateBackupAsync(string archiveFilePath)
    {
        if (!File.Exists(archiveFilePath))
            throw new FileNotFoundException($"Backup archive not found: {archiveFilePath}");

        using var zipStream = new FileStream(archiveFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var manifestEntry = archive.GetEntry("backup_manifest.json")
            ?? throw new VaultCorruptedException("Missing backup_manifest.json in archive.");

        BackupManifest manifest;
        using (var ms = manifestEntry.Open())
        {
            manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(ms).ConfigureAwait(false)
                ?? throw new VaultCorruptedException("Failed to parse backup manifest.");
        }

        if (manifest.ManifestVersion != 1)
        {
            throw new UnsupportedFormatException($"Unsupported backup manifest version: {manifest.ManifestVersion}");
        }

        // Verify header checksum
        var headerEntry = archive.GetEntry("vault.header")
            ?? throw new VaultCorruptedException("Archive missing vault.header.");
        using (var hs = headerEntry.Open())
        using (var mem = new MemoryStream())
        {
            await hs.CopyToAsync(mem).ConfigureAwait(false);
            string headerHash = Convert.ToHexString(SHA256.HashData(mem.ToArray()));
            if (!headerHash.Equals(manifest.HeaderSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new CryptographicIntegrityException("vault.header checksum mismatch in backup archive.");
            }
        }

        // Verify catalog checksum
        var catalogEntry = archive.GetEntry("catalog.db")
            ?? throw new VaultCorruptedException("Archive missing catalog.db.");
        using (var cs = catalogEntry.Open())
        using (var mem = new MemoryStream())
        {
            await cs.CopyToAsync(mem).ConfigureAwait(false);
            string catalogHash = Convert.ToHexString(SHA256.HashData(mem.ToArray()));
            if (!catalogHash.Equals(manifest.CatalogSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new CryptographicIntegrityException("catalog.db checksum mismatch in backup archive.");
            }
        }

        // Verify payload files
        foreach (var (relPath, expectedHash) in manifest.FileChecksums)
        {
            var fileEntry = archive.GetEntry(relPath)
                ?? throw new VaultCorruptedException($"Archive missing declared payload: {relPath}");

            using var fs = fileEntry.Open();
            using var mem = new MemoryStream();
            await fs.CopyToAsync(mem).ConfigureAwait(false);
            string actualHash = Convert.ToHexString(SHA256.HashData(mem.ToArray()));
            if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new CryptographicIntegrityException($"Checksum mismatch for payload file '{relPath}' in backup archive.");
            }
        }

        return manifest;
    }

    public async Task<VaultDescriptor> RestoreBackupAsync(
        string archiveFilePath, 
        string targetDirectoryPath, 
        string? newVaultName = null, 
        CancellationToken ct = default)
    {
        // 1. Verify archive validity and integrity first
        var manifest = await ValidateBackupAsync(archiveFilePath).ConfigureAwait(false);

        string vaultName = newVaultName ?? manifest.VaultName;
        string vaultDirName = vaultName.EndsWith(".pvault", StringComparison.OrdinalIgnoreCase) ? vaultName : $"{vaultName}.pvault";
        string targetVaultDir = Path.Combine(targetDirectoryPath, vaultDirName);

        if (Directory.Exists(targetVaultDir))
        {
            throw new PhantomVaultException($"Target restore directory already exists: {targetVaultDir}. Choose a new name or clean target directory.");
        }

        Directory.CreateDirectory(targetVaultDir);
        string dataDir = Path.Combine(targetVaultDir, "data");
        Directory.CreateDirectory(dataDir);

        using (var zipStream = new FileStream(archiveFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
        {
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName == "backup_manifest.json") continue;

                // Validate path to prevent Zip Slip
                string cleanRelative = StorageValidators.ValidateRelativePath(entry.FullName, targetVaultDir);
                string destPath = Path.Combine(targetVaultDir, cleanRelative);

                string? parentDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                entry.ExtractToFile(destPath, overwrite: true);
            }
        }

        // Open and register in vault repository
        return await _vaultRepo.OpenVaultAsync(targetVaultDir).ConfigureAwait(false);
    }
}
