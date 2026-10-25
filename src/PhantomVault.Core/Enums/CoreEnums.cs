namespace PhantomVault.Core.Enums;

/// <summary>
/// Represents the operational state of a secure vault.
/// </summary>
public enum VaultState
{
    /// <summary>
    /// Vault is closed and cryptographic keys have been zeroized in memory.
    /// </summary>
    Locked = 0,

    /// <summary>
    /// Vault is unlocked and accessible for file operations.
    /// </summary>
    Unlocked = 1,

    /// <summary>
    /// Vault requires user action (e.g., password change or migration).
    /// </summary>
    ActionRequired = 2,

    /// <summary>
    /// Vault detected an integrity issue and requires recovery.
    /// </summary>
    RecoveryRequired = 3
}

/// <summary>
/// Password-based key derivation functions supported by the application.
/// </summary>
public enum KdfAlgorithm
{
    /// <summary>
    /// Argon2id - Memory-hard password-based key derivation (RFC 9106).
    /// </summary>
    Argon2id = 1,

    /// <summary>
    /// PBKDF2 with HMAC-SHA-256 (RFC 8018) for compatible or fallback setups.
    /// </summary>
    Pbkdf2HmacSha256 = 2
}

/// <summary>
/// Supported media carrier types for steganographic embedding.
/// </summary>
public enum CarrierType
{
    PngImage = 1,
    WavAudio = 2
}

/// <summary>
/// Categorized types of local application audit events.
/// </summary>
public enum ActivityEventType
{
    VaultCreated,
    VaultUnlocked,
    VaultLocked,
    FileImported,
    FileExported,
    FileDeleted,
    FileRenamed,
    FilePurged,
    PasswordChanged,
    BackupCreated,
    BackupRestored,
    IntegrityCheckPassed,
    IntegrityCheckFailed,
    SecuritySettingsChanged,
    EmergencyLockTriggered,
    DuressUnlockTriggered,
    DecoyVaultCreated,
    StegoPayloadEmbedded,
    StegoPayloadExtracted
}

/// <summary>
/// Severity level of an audit activity event.
/// </summary>
public enum ActivitySeverity
{
    Info,
    Warning,
    SecurityAlert,
    Error
}
