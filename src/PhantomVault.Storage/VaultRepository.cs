using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using PhantomVault.Core.Enums;
using PhantomVault.Core.Exceptions;
using PhantomVault.Core.Interfaces;
using PhantomVault.Core.Models;
using PhantomVault.Security.SecureBuffering;

namespace PhantomVault.Storage;

/// <summary>
/// Manages multi-vault discovery, creation, unlocking, locking, and header maintenance.
/// </summary>
public sealed class VaultRepository : IVaultRepository
{
    private readonly ICryptographyService _crypto;
    private readonly IKeyDerivationService _kdf;
    private readonly IKeyWrappingService _keyWrapper;
    private readonly ConcurrentDictionary<Guid, SecureBuffer> _unlockedVaultKeys = new();
    private readonly List<VaultDescriptor> _vaultRegistry = new();
    private readonly object _registryLock = new();

    public VaultRepository(
        ICryptographyService crypto, 
        IKeyDerivationService kdf, 
        IKeyWrappingService keyWrapper)
    {
        _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
        _kdf = kdf ?? throw new ArgumentNullException(nameof(kdf));
        _keyWrapper = keyWrapper ?? throw new ArgumentNullException(nameof(keyWrapper));
        LoadRegistry();
    }

    public Task<IReadOnlyList<VaultDescriptor>> GetVaultsAsync()
    {
        lock (_registryLock)
        {
            // Update stats for any unlocked vaults
            return Task.FromResult<IReadOnlyList<VaultDescriptor>>(_vaultRegistry.ToArray());
        }
    }

    public async Task<VaultDescriptor> CreateVaultAsync(
        string name, 
        string storagePath, 
        string password, 
        KdfAlgorithm kdf = KdfAlgorithm.Argon2id)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new PhantomVaultException("Vault name cannot be empty.");

        if (string.IsNullOrEmpty(password))
            throw new PhantomVaultException("Master password cannot be empty.");

        string vaultDirName = name.EndsWith(".pvault", StringComparison.OrdinalIgnoreCase) ? name : $"{name}.pvault";
        string targetVaultDir = Path.Combine(storagePath, vaultDirName);

        if (Directory.Exists(targetVaultDir))
            throw new PhantomVaultException($"A vault directory already exists at: {targetVaultDir}");

        Directory.CreateDirectory(targetVaultDir);
        string dataDir = Path.Combine(targetVaultDir, "data");
        Directory.CreateDirectory(dataDir);

        // 1. Generate Master DEK (32 bytes)
        byte[] masterDek = _crypto.GenerateSecureRandomBytes(32);

        // 2. Derive KEK from password
        byte[] salt = _crypto.GenerateSecureRandomBytes(16);
        uint iterations = (kdf == KdfAlgorithm.Argon2id) ? 4u : 600_000u;
        uint memoryKiB = (kdf == KdfAlgorithm.Argon2id) ? 65536u : 0u;
        uint parallelism = (kdf == KdfAlgorithm.Argon2id) ? 4u : 0u;

        byte[] kek = _kdf.DeriveKey(password, salt, kdf, iterations, memoryKiB, parallelism);

        // 3. Wrap Master DEK
        var (wrappedKey, wrapNonce, wrapTag) = _keyWrapper.WrapKey(masterDek, kek);

        // 4. Construct and write vault.header binary
        var header = new VaultHeader
        {
            Version = 1,
            KdfAlgorithm = kdf,
            Salt = salt,
            Iterations = iterations,
            MemoryKiB = memoryKiB,
            Parallelism = parallelism,
            KeyWrapNonce = wrapNonce,
            WrappedKeyCiphertext = wrappedKey,
            HeaderAuthTag = wrapTag
        };

        string headerFilePath = Path.Combine(targetVaultDir, "vault.header");
        await WriteHeaderBinaryAsync(headerFilePath, header).ConfigureAwait(false);

        // 5. Initialize SQLite catalog
        var metaRepo = new MetadataRepository(targetVaultDir, _crypto);
        await metaRepo.InitializeSchemaAsync().ConfigureAwait(false);

        // 6. Create Descriptor and register
        var descriptor = new VaultDescriptor
        {
            Id = Guid.NewGuid(),
            Name = name,
            VaultPath = targetVaultDir,
            State = VaultState.Unlocked,
            KdfAlgorithm = kdf,
            CreatedAtUtc = DateTime.UtcNow,
            LastUnlockedAtUtc = DateTime.UtcNow
        };

        // Cache DEK in memory
        _unlockedVaultKeys[descriptor.Id] = new SecureBuffer(masterDek, zeroizeSource: true);

        lock (_registryLock)
        {
            _vaultRegistry.Add(descriptor);
            SaveRegistry();
        }

        return descriptor;
    }

    public async Task<VaultDescriptor> OpenVaultAsync(string vaultPath)
    {
        if (!Directory.Exists(vaultPath))
            throw new DirectoryNotFoundException($"Vault directory not found: {vaultPath}");

        string headerPath = Path.Combine(vaultPath, "vault.header");
        if (!File.Exists(headerPath))
            throw new VaultCorruptedException($"Missing vault.header in directory: {vaultPath}");

        VaultHeader header = await ReadHeaderBinaryAsync(headerPath).ConfigureAwait(false);

        string vaultName = Path.GetFileName(vaultPath);
        if (vaultName.EndsWith(".pvault", StringComparison.OrdinalIgnoreCase))
        {
            vaultName = vaultName.Substring(0, vaultName.Length - 7);
        }

        lock (_registryLock)
        {
            var existing = _vaultRegistry.Find(v => v.VaultPath.Equals(vaultPath, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                return existing;

            var descriptor = new VaultDescriptor
            {
                Id = Guid.NewGuid(),
                Name = vaultName,
                VaultPath = vaultPath,
                State = VaultState.Locked,
                KdfAlgorithm = header.KdfAlgorithm,
                CreatedAtUtc = File.GetCreationTimeUtc(headerPath)
            };

            _vaultRegistry.Add(descriptor);
            SaveRegistry();
            return descriptor;
        }
    }

    public async Task UnlockVaultAsync(Guid vaultId, string password)
    {
        VaultDescriptor? descriptor;
        lock (_registryLock)
        {
            descriptor = _vaultRegistry.Find(v => v.Id == vaultId);
        }

        if (descriptor == null)
            throw new PhantomVaultException("Vault descriptor not found.");

        string headerPath = Path.Combine(descriptor.VaultPath, "vault.header");
        if (!File.Exists(headerPath))
            throw new VaultCorruptedException("vault.header missing from vault directory.");

        VaultHeader header = await ReadHeaderBinaryAsync(headerPath).ConfigureAwait(false);

        // Derive KEK
        byte[] kek = _kdf.DeriveKey(
            password, 
            header.Salt, 
            header.KdfAlgorithm, 
            header.Iterations, 
            header.MemoryKiB, 
            header.Parallelism);

        // Unwrap Master DEK (throws InvalidCredentialsException on tag mismatch)
        byte[] masterDek = _keyWrapper.UnwrapKey(
            header.WrappedKeyCiphertext, 
            header.KeyWrapNonce, 
            header.HeaderAuthTag, 
            kek);

        // Store DEK in SecureBuffer
        if (_unlockedVaultKeys.TryGetValue(vaultId, out var existingBuffer))
        {
            existingBuffer.Dispose();
        }
        _unlockedVaultKeys[vaultId] = new SecureBuffer(masterDek, zeroizeSource: true);

        descriptor.State = VaultState.Unlocked;
        descriptor.LastUnlockedAtUtc = DateTime.UtcNow;

        // Refresh stats
        try
        {
            var metaRepo = new MetadataRepository(descriptor.VaultPath, _crypto);
            var (count, cipherBytes, plainBytes) = await metaRepo.GetStatsAsync().ConfigureAwait(false);
            descriptor.FileCount = count;
            descriptor.TotalCiphertextBytes = cipherBytes;
            descriptor.TotalPlaintextBytes = plainBytes;
        }
        catch { }

        lock (_registryLock)
        {
            SaveRegistry();
        }
    }

    public Task LockVaultAsync(Guid vaultId)
    {
        if (_unlockedVaultKeys.TryRemove(vaultId, out var buffer))
        {
            buffer.Dispose(); // Memory zeroized
        }

        lock (_registryLock)
        {
            var descriptor = _vaultRegistry.Find(v => v.Id == vaultId);
            if (descriptor != null)
            {
                descriptor.State = VaultState.Locked;
                SaveRegistry();
            }
        }

        return Task.CompletedTask;
    }

    public Task LockAllVaultsAsync()
    {
        foreach (var kvp in _unlockedVaultKeys)
        {
            kvp.Value.Dispose();
        }
        _unlockedVaultKeys.Clear();

        lock (_registryLock)
        {
            foreach (var v in _vaultRegistry)
            {
                v.State = VaultState.Locked;
            }
            SaveRegistry();
        }

        return Task.CompletedTask;
    }

    public async Task ChangePasswordAsync(Guid vaultId, string oldPassword, string newPassword)
    {
        VaultDescriptor? descriptor;
        lock (_registryLock)
        {
            descriptor = _vaultRegistry.Find(v => v.Id == vaultId);
        }

        if (descriptor == null)
            throw new PhantomVaultException("Vault not found.");

        string headerPath = Path.Combine(descriptor.VaultPath, "vault.header");
        VaultHeader header = await ReadHeaderBinaryAsync(headerPath).ConfigureAwait(false);

        // Verify old password
        byte[] oldKek = _kdf.DeriveKey(
            oldPassword, 
            header.Salt, 
            header.KdfAlgorithm, 
            header.Iterations, 
            header.MemoryKiB, 
            header.Parallelism);

        byte[] masterDek = _keyWrapper.UnwrapKey(
            header.WrappedKeyCiphertext, 
            header.KeyWrapNonce, 
            header.HeaderAuthTag, 
            oldKek);

        // Derive new KEK with fresh random salt
        byte[] newSalt = _crypto.GenerateSecureRandomBytes(16);
        byte[] newKek = _kdf.DeriveKey(
            newPassword, 
            newSalt, 
            header.KdfAlgorithm, 
            header.Iterations, 
            header.MemoryKiB, 
            header.Parallelism);

        // Re-wrap master DEK
        var (newWrapped, newNonce, newTag) = _keyWrapper.WrapKey(masterDek, newKek);

        header.Salt = newSalt;
        header.KeyWrapNonce = newNonce;
        header.WrappedKeyCiphertext = newWrapped;
        header.HeaderAuthTag = newTag;

        // Atomically replace header file
        string tempHeaderPath = headerPath + ".tmp";
        await WriteHeaderBinaryAsync(tempHeaderPath, header).ConfigureAwait(false);

        File.Move(tempHeaderPath, headerPath, overwrite: true);

        // Update active key in memory
        _unlockedVaultKeys[vaultId] = new SecureBuffer(masterDek, zeroizeSource: true);
    }

    public async Task<bool> VerifyVaultIntegrityAsync(Guid vaultId)
    {
        byte[]? key = GetVaultKey(vaultId);
        if (key == null)
            throw new VaultLockedException();

        VaultDescriptor? descriptor;
        lock (_registryLock)
        {
            descriptor = _vaultRegistry.Find(v => v.Id == vaultId);
        }
        if (descriptor == null) return false;

        var metaRepo = new MetadataRepository(descriptor.VaultPath, _crypto);
        var entries = await metaRepo.GetEntriesAsync(key).ConfigureAwait(false);

        foreach (var entry in entries)
        {
            string payloadPath = Path.Combine(descriptor.VaultPath, "data", $"{entry.Id}.pvc");
            if (!File.Exists(payloadPath)) return false;

            try
            {
                using var encStream = new FileStream(payloadPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var devNull = Stream.Null;
                await _crypto.DecryptStreamChunkedAsync(encStream, devNull, key, entry.Id).ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        return true;
    }

    public Task DeleteVaultAsync(Guid vaultId, bool deleteFilesFromDisk)
    {
        VaultDescriptor? descriptor;
        lock (_registryLock)
        {
            descriptor = _vaultRegistry.Find(v => v.Id == vaultId);
            if (descriptor != null)
            {
                _vaultRegistry.Remove(descriptor);
                SaveRegistry();
            }
        }

        if (descriptor != null)
        {
            if (_unlockedVaultKeys.TryRemove(vaultId, out var secBuf))
            {
                secBuf.Dispose();
            }

            if (deleteFilesFromDisk && Directory.Exists(descriptor.VaultPath))
            {
                try
                {
                    Directory.Delete(descriptor.VaultPath, recursive: true);
                }
                catch { }
            }
        }

        return Task.CompletedTask;
    }

    public bool IsVaultUnlocked(Guid vaultId) => _unlockedVaultKeys.ContainsKey(vaultId);

    public byte[]? GetVaultKey(Guid vaultId)
    {
        if (_unlockedVaultKeys.TryGetValue(vaultId, out var secBuf))
        {
            return secBuf.DangerousGetBuffer();
        }
        return null;
    }

    private static async Task WriteHeaderBinaryAsync(string filePath, VaultHeader header)
    {
        // Total fixed header length:
        // Magic (4B) + Version (2B) + KdfAlgorithm (2B) + Salt (16B) + 
        // Iterations (4B) + MemoryKiB (4B) + Parallelism (4B) + 
        // KeyWrapNonce (12B) + WrappedKey (32B) + Tag (16B) = 96 bytes
        byte[] buffer = new byte[96];
        int offset = 0;

        VaultHeader.MagicBytes.CopyTo(buffer.AsSpan(offset, 4));
        offset += 4;

        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset, 2), header.Version);
        offset += 2;

        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset, 2), (ushort)header.KdfAlgorithm);
        offset += 2;

        header.Salt.CopyTo(buffer.AsSpan(offset, 16));
        offset += 16;

        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset, 4), header.Iterations);
        offset += 4;

        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset, 4), header.MemoryKiB);
        offset += 4;

        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset, 4), header.Parallelism);
        offset += 4;

        header.KeyWrapNonce.CopyTo(buffer.AsSpan(offset, 12));
        offset += 12;

        header.WrappedKeyCiphertext.CopyTo(buffer.AsSpan(offset, 32));
        offset += 32;

        header.HeaderAuthTag.CopyTo(buffer.AsSpan(offset, 16));
        offset += 16;

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await fs.WriteAsync(buffer).ConfigureAwait(false);
        await fs.FlushAsync().ConfigureAwait(false);
    }

    private static async Task<VaultHeader> ReadHeaderBinaryAsync(string filePath)
    {
        byte[] buffer = new byte[96];
        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            int totalRead = 0;
            while (totalRead < 96)
            {
                int read = await fs.ReadAsync(buffer.AsMemory(totalRead, 96 - totalRead)).ConfigureAwait(false);
                if (read == 0) throw new VaultCorruptedException("Corrupt vault header: premature EOF.");
                totalRead += read;
            }
        }

        int offset = 0;
        if (!buffer.AsSpan(0, 4).SequenceEqual(VaultHeader.MagicBytes))
            throw new VaultCorruptedException("Invalid vault header: magic bytes mismatch.");
        offset += 4;

        ushort version = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(offset, 2));
        offset += 2;
        if (version != 1)
            throw new UnsupportedFormatException($"Unsupported vault header version: {version}");

        ushort kdfId = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(offset, 2));
        offset += 2;

        byte[] salt = new byte[16];
        buffer.AsSpan(offset, 16).CopyTo(salt);
        offset += 16;

        uint iterations = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(offset, 4));
        offset += 4;

        uint memoryKiB = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(offset, 4));
        offset += 4;

        uint parallelism = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(offset, 4));
        offset += 4;

        byte[] nonce = new byte[12];
        buffer.AsSpan(offset, 12).CopyTo(nonce);
        offset += 12;

        byte[] wrappedKey = new byte[32];
        buffer.AsSpan(offset, 32).CopyTo(wrappedKey);
        offset += 32;

        byte[] tag = new byte[16];
        buffer.AsSpan(offset, 16).CopyTo(tag);
        offset += 16;

        return new VaultHeader
        {
            Version = version,
            KdfAlgorithm = (KdfAlgorithm)kdfId,
            Salt = salt,
            Iterations = iterations,
            MemoryKiB = memoryKiB,
            Parallelism = parallelism,
            KeyWrapNonce = nonce,
            WrappedKeyCiphertext = wrappedKey,
            HeaderAuthTag = tag
        };
    }

    private void LoadRegistry()
    {
        lock (_registryLock)
        {
            try
            {
                string regPath = StoragePaths.VaultRegistryPath;
                if (File.Exists(regPath))
                {
                    string json = File.ReadAllText(regPath);
                    var list = JsonSerializer.Deserialize<List<VaultDescriptor>>(json);
                    if (list != null)
                    {
                        _vaultRegistry.Clear();
                        foreach (var v in list)
                        {
                            v.State = VaultState.Locked; // Always starts locked on application startup
                            if (Directory.Exists(v.VaultPath))
                            {
                                _vaultRegistry.Add(v);
                            }
                        }
                    }
                }
            }
            catch { }
        }
    }

    private void SaveRegistry()
    {
        lock (_registryLock)
        {
            try
            {
                string regPath = StoragePaths.VaultRegistryPath;
                string json = JsonSerializer.Serialize(_vaultRegistry, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(regPath, json);
            }
            catch { }
        }
    }
}
