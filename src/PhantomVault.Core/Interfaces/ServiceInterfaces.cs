using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PhantomVault.Core.Enums;
using PhantomVault.Core.Models;

namespace PhantomVault.Core.Interfaces;

/// <summary>
/// Authenticated cryptographic primitives and chunked stream encryption.
/// </summary>
public interface ICryptographyService
{
    byte[] GenerateSecureRandomBytes(int length);
    
    byte[] EncryptBytes(
        ReadOnlySpan<byte> plaintext, 
        ReadOnlySpan<byte> key, 
        ReadOnlySpan<byte> nonce, 
        ReadOnlySpan<byte> associatedData, 
        out byte[] tag);

    byte[] DecryptBytes(
        ReadOnlySpan<byte> ciphertext, 
        ReadOnlySpan<byte> key, 
        ReadOnlySpan<byte> nonce, 
        ReadOnlySpan<byte> tag, 
        ReadOnlySpan<byte> associatedData);

    Task EncryptStreamChunkedAsync(
        Stream inputStream, 
        Stream outputStream, 
        byte[] key, 
        Guid fileId, 
        IProgress<double>? progress = null, 
        CancellationToken cancellationToken = default);

    Task DecryptStreamChunkedAsync(
        Stream inputStream, 
        Stream outputStream, 
        byte[] key, 
        Guid fileId, 
        IProgress<double>? progress = null, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Password-based key derivation service.
/// </summary>
public interface IKeyDerivationService
{
    byte[] DeriveKey(
        string password, 
        byte[] salt, 
        KdfAlgorithm algorithm, 
        uint iterations = 4, 
        uint memoryKiB = 65536, 
        uint parallelism = 4);
}

/// <summary>
/// Encapsulates wrapping and unwrapping of vault master keys with KEKs.
/// </summary>
public interface IKeyWrappingService
{
    (byte[] wrappedKey, byte[] nonce, byte[] tag) WrapKey(ReadOnlySpan<byte> masterKey, ReadOnlySpan<byte> kek);
    byte[] UnwrapKey(ReadOnlySpan<byte> wrappedKey, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> tag, ReadOnlySpan<byte> kek);
}

/// <summary>
/// Manages vault discovery, creation, unlocking, locking, and integrity verification.
/// </summary>
public interface IVaultRepository
{
    Task<IReadOnlyList<VaultDescriptor>> GetVaultsAsync();
    Task<VaultDescriptor> CreateVaultAsync(string name, string storagePath, string password, KdfAlgorithm kdf = KdfAlgorithm.Argon2id);
    Task<VaultDescriptor> OpenVaultAsync(string vaultPath);
    Task UnlockVaultAsync(Guid vaultId, string password);
    Task LockVaultAsync(Guid vaultId);
    Task LockAllVaultsAsync();
    Task ChangePasswordAsync(Guid vaultId, string oldPassword, string newPassword);
    Task<bool> VerifyVaultIntegrityAsync(Guid vaultId);
    Task DeleteVaultAsync(Guid vaultId, bool deleteFilesFromDisk);
    bool IsVaultUnlocked(Guid vaultId);
    byte[]? GetVaultKey(Guid vaultId);
}

/// <summary>
/// Handles atomic, chunked, encrypted file import, export, and deletion within an unlocked vault.
/// </summary>
public interface IVaultFileManager
{
    Task<VaultEntry> ImportFileAsync(Guid vaultId, string sourceFilePath, Guid? parentFolderId = null, IProgress<double>? progress = null, CancellationToken ct = default);
    Task ExportFileAsync(Guid vaultId, Guid fileEntryId, string destinationFilePath, IProgress<double>? progress = null, CancellationToken ct = default);
    Task<byte[]> GetDecryptedFileBytesAsync(Guid vaultId, Guid fileEntryId, IProgress<double>? progress = null, CancellationToken ct = default);
    Task DeleteEntryAsync(Guid vaultId, Guid entryId);
    Task RenameEntryAsync(Guid vaultId, Guid entryId, string newName);
    Task<IReadOnlyList<VaultEntry>> GetEntriesAsync(Guid vaultId, Guid? parentFolderId = null);
}

/// <summary>
/// Image and audio steganography service for embedding and extracting authenticated encrypted payloads.
/// </summary>
public interface ISteganographyService
{
    Task<StegoCarrierInfo> AnalyzeCarrierAsync(string carrierFilePath);
    Task<string> EmbedPayloadAsync(string carrierFilePath, string payloadFilePath, string password, string outputFilePath, IProgress<double>? progress = null, CancellationToken ct = default);
    Task<StegoPayloadInfo> ExtractPayloadAsync(string carrierFilePath, string password, string outputDirectoryPath, IProgress<double>? progress = null, CancellationToken ct = default);
}

/// <summary>
/// Encrypted backup packaging, manifest verification, and disaster recovery.
/// </summary>
public interface IBackupService
{
    Task<string> CreateBackupAsync(Guid vaultId, string destinationArchivePath, IProgress<double>? progress = null, CancellationToken ct = default);
    Task<BackupManifest> ValidateBackupAsync(string archiveFilePath);
    Task<VaultDescriptor> RestoreBackupAsync(string archiveFilePath, string targetDirectoryPath, string? newVaultName = null, CancellationToken ct = default);
}

/// <summary>
/// Local, privacy-preserving audit logging with automated data scrubbing.
/// </summary>
public interface IActivityLogService
{
    Task LogEventAsync(ActivityEventType eventType, ActivitySeverity severity, string vaultName, string message, string details = "");
    Task<IReadOnlyList<ActivityLogRecord>> GetLogsAsync(ActivityEventType? filterType = null, int maxCount = 500);
    Task ClearLogsAsync();
}

/// <summary>
/// Manages inactivity timeouts, workstation lock triggers, and dead-man's switch countdowns.
/// </summary>
public interface ISecurityPolicyService
{
    EmergencyPolicy CurrentPolicy { get; }
    void UpdatePolicy(EmergencyPolicy policy);
    void RegisterUserActivity();
    void TriggerEmergencyLock();
    event Action<int>? DeadMansSwitchCountdownTick;
    event Action? EmergencyLockTriggered;
}

/// <summary>
/// Evaluates password entropy, complexity, and common vulnerability rules.
/// </summary>
public interface IPasswordPolicyValidator
{
    PasswordStrengthResult EvaluatePassword(string password);
}

/// <summary>
/// Securely overwrites and destroys plaintext files on disk according to DoD 5220.22-M sanitization standards.
/// </summary>
public interface ISecureShredderService
{
    Task ShredFileAsync(string filePath, int passes = 3, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Hardware-backed biometric / PIN authentication via Windows Hello / UserConsentVerifier.
/// </summary>
public interface IWindowsHelloService
{
    Task<bool> IsAvailableAsync();
    Task<bool> VerifyUserAsync(string promptMessage);
}

/// <summary>
/// Tactical cybersecurity audio synthesis and acoustic feedback service.
/// Plays pure mathematically generated in-memory PCM WAV streams.
/// </summary>
public interface ISoundEffectService
{
    bool IsSoundEnabled { get; set; }
    void PlayUnlockSound();
    void PlayLockSound();
    void PlayEmergencySound();
    void PlayShredSound();
    void PlayStegoSound();
}

/// <summary>
/// Provides plausible deniability under physical duress or coercion.
/// Configures and manages secondary decoy vaults tied to duress passwords.
/// </summary>
public interface IDecoyVaultService
{
    Task<bool> HasDuressPasswordAsync(Guid vaultId);
    Task SetDuressPasswordAsync(Guid vaultId, string duressPassword);
    Task<bool> VerifyIsDuressPasswordAsync(Guid vaultId, string candidatePassword);
    Task<VaultDescriptor> EnsureDecoyVaultAsync(Guid masterVaultId, string duressPassword);
    Task PopulateRealisticDecoyFilesAsync(Guid decoyVaultId);
}

