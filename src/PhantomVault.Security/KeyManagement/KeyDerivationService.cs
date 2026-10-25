using System;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using PhantomVault.Core.Enums;
using PhantomVault.Core.Exceptions;
using PhantomVault.Core.Interfaces;

namespace PhantomVault.Security.KeyManagement;

/// <summary>
/// Password-based key derivation service supporting Argon2id and PBKDF2-HMAC-SHA256.
/// </summary>
public sealed class KeyDerivationService : IKeyDerivationService
{
    public const int DefaultDerivedKeyLength = 32; // 256 bits for AES-256
    public const int MinimumSaltLength = 16;       // 128 bits
    public const uint DefaultArgon2Iterations = 4;
    public const uint DefaultArgon2MemoryKiB = 65536; // 64 MiB
    public const uint DefaultArgon2Parallelism = 4;
    public const int DefaultPbkdf2Iterations = 600_000;

    public byte[] DeriveKey(
        string password, 
        byte[] salt, 
        KdfAlgorithm algorithm, 
        uint iterations = DefaultArgon2Iterations, 
        uint memoryKiB = DefaultArgon2MemoryKiB, 
        uint parallelism = DefaultArgon2Parallelism)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be empty.", nameof(password));

        if (salt == null || salt.Length < MinimumSaltLength)
            throw new ArgumentException($"Salt must be at least {MinimumSaltLength} bytes long.", nameof(salt));

        switch (algorithm)
        {
            case KdfAlgorithm.Argon2id:
                return DeriveArgon2id(password, salt, iterations, memoryKiB, parallelism);

            case KdfAlgorithm.Pbkdf2HmacSha256:
                int pbkdf2Rounds = (iterations > 0) ? (int)iterations : DefaultPbkdf2Iterations;
                return DerivePbkdf2(password, salt, pbkdf2Rounds);

            default:
                throw new UnsupportedFormatException($"Unsupported KDF algorithm identifier: {algorithm}");
        }
    }

    private static byte[] DeriveArgon2id(
        string password, 
        byte[] salt, 
        uint iterations, 
        uint memoryKiB, 
        uint parallelism)
    {
        if (iterations < 1)
            throw new ArgumentOutOfRangeException(nameof(iterations), "Argon2id iterations must be at least 1.");

        if (memoryKiB < 1024)
            throw new ArgumentOutOfRangeException(nameof(memoryKiB), "Argon2id memory cost must be at least 1,024 KiB (1 MiB).");

        if (parallelism < 1)
            throw new ArgumentOutOfRangeException(nameof(parallelism), "Argon2id degree of parallelism must be at least 1.");

        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                DegreeOfParallelism = (int)parallelism,
                Iterations = (int)iterations,
                MemorySize = (int)memoryKiB
            };

            return argon2.GetBytes(DefaultDerivedKeyLength);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    private static byte[] DerivePbkdf2(string password, byte[] salt, int iterations)
    {
        if (iterations < 100_000)
            throw new ArgumentOutOfRangeException(nameof(iterations), "PBKDF2 iteration count must be at least 100,000 rounds.");

        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                passwordBytes, 
                salt, 
                iterations, 
                HashAlgorithmName.SHA256, 
                DefaultDerivedKeyLength);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }
}
