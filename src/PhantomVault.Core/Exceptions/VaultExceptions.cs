using System;

namespace PhantomVault.Core.Exceptions;

/// <summary>
/// Base exception for all PhantomVault specific domain and operational errors.
/// </summary>
public class PhantomVaultException : Exception
{
    public PhantomVaultException(string message) : base(message) { }
    public PhantomVaultException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when cryptographic authentication tags fail, data has been tampered with,
/// or ciphertext fails authenticated integrity verification.
/// </summary>
public class CryptographicIntegrityException : PhantomVaultException
{
    public CryptographicIntegrityException(string message) 
        : base(message) { }

    public CryptographicIntegrityException(string message, Exception innerException) 
        : base(message, innerException) { }
}

/// <summary>
/// Thrown when the user provides an invalid password or authentication fails.
/// </summary>
public class InvalidCredentialsException : PhantomVaultException
{
    public InvalidCredentialsException(string message = "The specified password or authentication secret is incorrect.") 
        : base(message) { }

    public InvalidCredentialsException(string message, Exception innerException) 
        : base(message, innerException) { }
}

/// <summary>
/// Thrown when an operation requiring an active key is invoked while the vault is locked.
/// </summary>
public class VaultLockedException : PhantomVaultException
{
    public VaultLockedException(string message = "The vault is currently locked. Authenticate first to access contents.") 
        : base(message) { }
}

/// <summary>
/// Thrown when vault structure, headers, or metadata fail format validation.
/// </summary>
public class VaultCorruptedException : PhantomVaultException
{
    public VaultCorruptedException(string message) : base(message) { }
    public VaultCorruptedException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a steganographic carrier cannot accommodate the encrypted payload.
/// </summary>
public class CarrierCapacityExceededException : PhantomVaultException
{
    public long RequiredBytes { get; }
    public long AvailableBytes { get; }

    public CarrierCapacityExceededException(long requiredBytes, long availableBytes)
        : base($"Payload exceeds carrier capacity. Required: {requiredBytes} bytes, Available: {availableBytes} bytes.")
    {
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
    }
}

/// <summary>
/// Thrown when an input path contains directory traversal tokens (e.g. '../', '..\\').
/// </summary>
public class PathTraversalException : PhantomVaultException
{
    public PathTraversalException(string path) 
        : base($"Potential path traversal attempt detected for path: '{path}'") { }
}

/// <summary>
/// Thrown when a file, image, or audio container has an unsupported or unrecognized format.
/// </summary>
public class UnsupportedFormatException : PhantomVaultException
{
    public UnsupportedFormatException(string message) : base(message) { }
}
