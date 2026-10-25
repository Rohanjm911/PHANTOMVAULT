using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using PhantomVault.Core.Enums;

namespace PhantomVault.Core.Models;

/// <summary>
/// Descriptor representing a known local vault and its status.
/// </summary>
public class VaultDescriptor : INotifyPropertyChanged
{
    private VaultState _state = VaultState.Locked;
    private int _fileCount;
    private long _totalPlaintextBytes;
    private long _totalCiphertextBytes;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string VaultPath { get; set; } = string.Empty;

    public VaultState State
    {
        get => _state;
        set
        {
            if (_state != value)
            {
                _state = value;
                OnPropertyChanged(nameof(State));
                OnPropertyChanged(nameof(IsUnlocked));
            }
        }
    }

    [JsonIgnore]
    public bool IsUnlocked => State == VaultState.Unlocked;

    public KdfAlgorithm KdfAlgorithm { get; set; } = KdfAlgorithm.Argon2id;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastUnlockedAtUtc { get; set; }

    public int FileCount
    {
        get => _fileCount;
        set
        {
            if (_fileCount != value)
            {
                _fileCount = value;
                OnPropertyChanged(nameof(FileCount));
            }
        }
    }

    public long TotalPlaintextBytes
    {
        get => _totalPlaintextBytes;
        set
        {
            if (_totalPlaintextBytes != value)
            {
                _totalPlaintextBytes = value;
                OnPropertyChanged(nameof(TotalPlaintextBytes));
            }
        }
    }

    public long TotalCiphertextBytes
    {
        get => _totalCiphertextBytes;
        set
        {
            if (_totalCiphertextBytes != value)
            {
                _totalCiphertextBytes = value;
                OnPropertyChanged(nameof(TotalCiphertextBytes));
            }
        }
    }

    public bool AutoLockEnabled { get; set; } = true;
    public int AutoLockTimeoutMinutes { get; set; } = 15;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Represents the parsed binary header stored at the root of a vault directory.
/// </summary>
public class VaultHeader
{
    public static readonly byte[] MagicBytes = new byte[] { 0x50, 0x56, 0x4C, 0x54 }; // "PVLT"
    public const ushort CurrentVersion = 1;

    public ushort Version { get; set; } = CurrentVersion;
    public KdfAlgorithm KdfAlgorithm { get; set; } = KdfAlgorithm.Argon2id;
    public byte[] Salt { get; set; } = Array.Empty<byte>();
    public uint Iterations { get; set; }
    public uint MemoryKiB { get; set; }
    public uint Parallelism { get; set; }
    public byte[] KeyWrapNonce { get; set; } = Array.Empty<byte>();
    public byte[] WrappedKeyCiphertext { get; set; } = Array.Empty<byte>();
    public byte[] HeaderAuthTag { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Represents a stored file or folder entry inside a vault.
/// </summary>
public class VaultEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public long PlaintextSizeBytes { get; set; }
    public long CiphertextSizeBytes { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;
    public string? MimeType { get; set; }
}

/// <summary>
/// Inspection report for an image or audio carrier before steganographic embedding.
/// </summary>
public class StegoCarrierInfo
{
    public string FilePath { get; set; } = string.Empty;
    public CarrierType CarrierType { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int Channels { get; set; }
    public long TotalCarrierSizeBytes { get; set; }
    public long UsableCapacityBytes { get; set; }
    public bool IsSuitable { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
}

/// <summary>
/// Metadata describing an extracted or prepared steganographic payload.
/// </summary>
public class StegoPayloadInfo
{
    public string OriginalFileName { get; set; } = string.Empty;
    public long PayloadSizeBytes { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Integrity manifest embedded in an encrypted .pvbackup package.
/// </summary>
public class BackupManifest
{
    public const int CurrentManifestVersion = 1;

    public int ManifestVersion { get; set; } = CurrentManifestVersion;
    public Guid VaultId { get; set; }
    public string VaultName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string AppVersion { get; set; } = "1.0.0";
    public int FileCount { get; set; }
    public long TotalCiphertextBytes { get; set; }
    public string HeaderSha256 { get; set; } = string.Empty;
    public string CatalogSha256 { get; set; } = string.Empty;
    public Dictionary<string, string> FileChecksums { get; set; } = new();
    public string ArchiveChecksum { get; set; } = string.Empty;
}

/// <summary>
/// Sanitized audit log record of a security or operational event.
/// </summary>
public class ActivityLogRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public ActivityEventType EventType { get; set; }
    public ActivitySeverity Severity { get; set; } = ActivitySeverity.Info;
    public string VaultName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

/// <summary>
/// User-configurable policy for automatic and emergency vault protection.
/// </summary>
public class EmergencyPolicy
{
    public bool AutoLockOnInactivity { get; set; } = true;
    public int InactivityMinutes { get; set; } = 15;
    public bool LockOnWorkstationLock { get; set; } = true;
    public bool DeadMansSwitchEnabled { get; set; } = false;
    public int DeadMansSwitchTimeoutMinutes { get; set; } = 60;
    public int WarningCountdownSeconds { get; set; } = 30;
    public bool IsTestMode { get; set; } = false;
}

/// <summary>
/// Result of evaluating password complexity and entropy.
/// </summary>
public class PasswordStrengthResult
{
    public int Score { get; set; } // 0 to 4
    public string Label { get; set; } = "Very Weak";
    public double EstimatedEntropyBits { get; set; }
    public List<string> Suggestions { get; set; } = new();
    public bool MeetsRequirements => Score >= 3;
}
