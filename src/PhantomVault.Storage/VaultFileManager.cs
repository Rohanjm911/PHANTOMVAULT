using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using PhantomVault.Core.Exceptions;
using PhantomVault.Core.Interfaces;
using PhantomVault.Core.Models;
using PhantomVault.Core.Validation;

namespace PhantomVault.Storage;

/// <summary>
/// Manages atomic chunked file import, export, and deletion within an unlocked vault.
/// </summary>
public sealed class VaultFileManager : IVaultFileManager
{
    private readonly IVaultRepository _vaultRepo;
    private readonly ICryptographyService _crypto;

    public VaultFileManager(IVaultRepository vaultRepo, ICryptographyService crypto)
    {
        _vaultRepo = vaultRepo ?? throw new ArgumentNullException(nameof(vaultRepo));
        _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
    }

    public async Task<VaultEntry> ImportFileAsync(
        Guid vaultId, 
        string sourceFilePath, 
        Guid? parentFolderId = null, 
        IProgress<double>? progress = null, 
        CancellationToken ct = default)
    {
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException($"Source file not found: {sourceFilePath}");

        byte[]? vaultKey = _vaultRepo.GetVaultKey(vaultId);
        if (vaultKey == null)
            throw new VaultLockedException();

        var vaults = await _vaultRepo.GetVaultsAsync().ConfigureAwait(false);
        var vault = Array.Find(vaults.ToArray(), v => v.Id == vaultId) 
            ?? throw new PhantomVaultException("Vault descriptor not found.");

        string fileName = StorageValidators.ValidateAndSanitizeFileName(Path.GetFileName(sourceFilePath));
        Guid fileId = Guid.NewGuid();

        string dataDir = Path.Combine(vault.VaultPath, "data");
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }

        string tempPayloadPath = Path.Combine(dataDir, $"{fileId}.pvc.tmp");
        string finalPayloadPath = Path.Combine(dataDir, $"{fileId}.pvc");

        long plaintextSize = 0;
        string sha256Hex = string.Empty;

        try
        {
            // 1. Calculate plaintext SHA-256 and encrypt in chunked stream
            using (var srcStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var hashStream = new HashingReadStream(srcStream))
            using (var destStream = new FileStream(tempPayloadPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                plaintextSize = srcStream.Length;
                await _crypto.EncryptStreamChunkedAsync(hashStream, destStream, vaultKey, fileId, progress, ct).ConfigureAwait(false);
                sha256Hex = Convert.ToHexString(hashStream.GetHash());
            }

            // 2. Commit atomic file rename
            if (File.Exists(finalPayloadPath))
            {
                File.Delete(finalPayloadPath);
            }
            File.Move(tempPayloadPath, finalPayloadPath);

            long ciphertextSize = new FileInfo(finalPayloadPath).Length;

            // 3. Insert metadata record in catalog
            var entry = new VaultEntry
            {
                Id = fileId,
                ParentId = parentFolderId,
                Name = fileName,
                RelativePath = fileName,
                PlaintextSizeBytes = plaintextSize,
                CiphertextSizeBytes = ciphertextSize,
                Sha256Hash = sha256Hex,
                IsDirectory = false,
                CreatedAtUtc = DateTime.UtcNow,
                ModifiedAtUtc = DateTime.UtcNow,
                MimeType = GetMimeType(fileName)
            };

            var metaRepo = new MetadataRepository(vault.VaultPath, _crypto);
            await metaRepo.AddEntryAsync(entry, vaultKey).ConfigureAwait(false);

            return entry;
        }
        catch
        {
            if (File.Exists(tempPayloadPath))
            {
                try { File.Delete(tempPayloadPath); } catch { }
            }
            throw;
        }
    }

    public async Task ExportFileAsync(
        Guid vaultId, 
        Guid fileEntryId, 
        string destinationFilePath, 
        IProgress<double>? progress = null, 
        CancellationToken ct = default)
    {
        byte[]? vaultKey = _vaultRepo.GetVaultKey(vaultId);
        if (vaultKey == null)
            throw new VaultLockedException();

        var vaults = await _vaultRepo.GetVaultsAsync().ConfigureAwait(false);
        var vault = Array.Find(vaults.ToArray(), v => v.Id == vaultId) 
            ?? throw new PhantomVaultException("Vault descriptor not found.");

        var metaRepo = new MetadataRepository(vault.VaultPath, _crypto);
        var entry = await metaRepo.GetEntryByIdAsync(fileEntryId, vaultKey).ConfigureAwait(false)
            ?? throw new PhantomVaultException("File entry not found in vault catalog.");

        string payloadPath = Path.Combine(vault.VaultPath, "data", $"{fileEntryId}.pvc");
        if (!File.Exists(payloadPath))
            throw new VaultCorruptedException($"Encrypted payload missing on disk: {payloadPath}");

        string destDir = Path.GetDirectoryName(destinationFilePath) ?? string.Empty;
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        string tempDestPath = destinationFilePath + ".tmp";

        try
        {
            string computedHashHex = string.Empty;

            using (var encStream = new FileStream(payloadPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var destStream = new FileStream(tempDestPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var hashingWrite = new HashingWriteStream(destStream))
            {
                await _crypto.DecryptStreamChunkedAsync(encStream, hashingWrite, vaultKey, fileEntryId, progress, ct).ConfigureAwait(false);
                computedHashHex = Convert.ToHexString(hashingWrite.GetHash());
            }

            // End-to-end integrity check
            if (!computedHashHex.Equals(entry.Sha256Hash, StringComparison.OrdinalIgnoreCase))
            {
                throw new CryptographicIntegrityException("Post-decryption SHA-256 verification failed. File integrity has been compromised.");
            }

            // Atomic commit
            if (File.Exists(destinationFilePath))
            {
                File.Delete(destinationFilePath);
            }
            File.Move(tempDestPath, destinationFilePath);
        }
        catch
        {
            if (File.Exists(tempDestPath))
            {
                try { File.Delete(tempDestPath); } catch { }
            }
            throw;
        }
    }

    public async Task<byte[]> GetDecryptedFileBytesAsync(
        Guid vaultId, 
        Guid fileEntryId, 
        IProgress<double>? progress = null, 
        CancellationToken ct = default)
    {
        byte[]? vaultKey = _vaultRepo.GetVaultKey(vaultId);
        if (vaultKey == null)
            throw new VaultLockedException();

        var vaults = await _vaultRepo.GetVaultsAsync().ConfigureAwait(false);
        var vault = Array.Find(vaults.ToArray(), v => v.Id == vaultId) 
            ?? throw new PhantomVaultException("Vault descriptor not found.");

        var metaRepo = new MetadataRepository(vault.VaultPath, _crypto);
        var entry = await metaRepo.GetEntryByIdAsync(fileEntryId, vaultKey).ConfigureAwait(false)
            ?? throw new PhantomVaultException("File entry not found in vault catalog.");

        string payloadPath = Path.Combine(vault.VaultPath, "data", $"{fileEntryId}.pvc");
        if (!File.Exists(payloadPath))
            throw new VaultCorruptedException($"Encrypted payload missing on disk: {payloadPath}");

        using var memoryStream = new MemoryStream((int)Math.Min(entry.PlaintextSizeBytes, 100 * 1024 * 1024));
        using (var encStream = new FileStream(payloadPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var hashingWrite = new HashingWriteStream(memoryStream))
        {
            await _crypto.DecryptStreamChunkedAsync(encStream, hashingWrite, vaultKey, fileEntryId, progress, ct).ConfigureAwait(false);
            string computedHashHex = Convert.ToHexString(hashingWrite.GetHash());

            if (!computedHashHex.Equals(entry.Sha256Hash, StringComparison.OrdinalIgnoreCase))
            {
                throw new CryptographicIntegrityException("Post-decryption SHA-256 verification failed. File integrity compromised.");
            }
        }

        return memoryStream.ToArray();
    }

    public async Task DeleteEntryAsync(Guid vaultId, Guid entryId)
    {
        byte[]? vaultKey = _vaultRepo.GetVaultKey(vaultId);
        if (vaultKey == null)
            throw new VaultLockedException();

        var vaults = await _vaultRepo.GetVaultsAsync().ConfigureAwait(false);
        var vault = Array.Find(vaults.ToArray(), v => v.Id == vaultId) 
            ?? throw new PhantomVaultException("Vault descriptor not found.");

        var metaRepo = new MetadataRepository(vault.VaultPath, _crypto);
        await metaRepo.DeleteEntryAsync(entryId).ConfigureAwait(false);

        string payloadPath = Path.Combine(vault.VaultPath, "data", $"{entryId}.pvc");
        if (File.Exists(payloadPath))
        {
            try
            {
                File.Delete(payloadPath);
            }
            catch { }
        }
    }

    public async Task RenameEntryAsync(Guid vaultId, Guid entryId, string newName)
    {
        byte[]? vaultKey = _vaultRepo.GetVaultKey(vaultId);
        if (vaultKey == null)
            throw new VaultLockedException();

        var vaults = await _vaultRepo.GetVaultsAsync().ConfigureAwait(false);
        var vault = Array.Find(vaults.ToArray(), v => v.Id == vaultId) 
            ?? throw new PhantomVaultException("Vault descriptor not found.");

        string sanitized = StorageValidators.ValidateAndSanitizeFileName(newName);
        var metaRepo = new MetadataRepository(vault.VaultPath, _crypto);
        await metaRepo.RenameEntryAsync(entryId, sanitized, vaultKey).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<VaultEntry>> GetEntriesAsync(Guid vaultId, Guid? parentFolderId = null)
    {
        byte[]? vaultKey = _vaultRepo.GetVaultKey(vaultId);
        if (vaultKey == null)
            throw new VaultLockedException();

        var vaults = await _vaultRepo.GetVaultsAsync().ConfigureAwait(false);
        var vault = Array.Find(vaults.ToArray(), v => v.Id == vaultId) 
            ?? throw new PhantomVaultException("Vault descriptor not found.");

        var metaRepo = new MetadataRepository(vault.VaultPath, _crypto);
        return await metaRepo.GetEntriesAsync(vaultKey, parentFolderId).ConfigureAwait(false);
    }

    private static string GetMimeType(string fileName)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".txt" or ".log" => "text/plain",
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".zip" => "application/zip",
            ".json" => "application/json",
            ".csv" => "text/csv",
            _ => "application/octet-stream"
        };
    }

    private sealed class HashingReadStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly IncrementalHash _hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        public HashingReadStream(Stream baseStream) => _baseStream = baseStream;

        public byte[] GetHash() => _hasher.GetHashAndReset();

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _baseStream.Length;
        public override long Position { get => _baseStream.Position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = _baseStream.Read(buffer, offset, count);
            if (read > 0) _hasher.AppendData(buffer, offset, read);
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int read = await _baseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read > 0) _hasher.AppendData(buffer.Span.Slice(0, read));
            return read;
        }

        public override void Flush() => _baseStream.Flush();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) _hasher.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class HashingWriteStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly IncrementalHash _hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        public HashingWriteStream(Stream baseStream) => _baseStream = baseStream;

        public byte[] GetHash() => _hasher.GetHashAndReset();

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => _baseStream.CanWrite;
        public override long Length => _baseStream.Length;
        public override long Position { get => _baseStream.Position; set => throw new NotSupportedException(); }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _baseStream.Write(buffer, offset, count);
            _hasher.AppendData(buffer, offset, count);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _baseStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            _hasher.AppendData(buffer.Span);
        }

        public override void Flush() => _baseStream.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _baseStream.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) _hasher.Dispose();
            base.Dispose(disposing);
        }
    }
}
