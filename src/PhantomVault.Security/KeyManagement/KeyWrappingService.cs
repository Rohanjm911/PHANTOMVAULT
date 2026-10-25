using System;
using System.Security.Cryptography;
using System.Text;
using PhantomVault.Core.Exceptions;
using PhantomVault.Core.Interfaces;

namespace PhantomVault.Security.KeyManagement;

/// <summary>
/// Cryptographic key wrapping service protecting Master Data Encryption Keys (DEKs)
/// with password-derived Key Encryption Keys (KEKs) via AES-256-GCM.
/// </summary>
public sealed class KeyWrappingService : IKeyWrappingService
{
    private static readonly byte[] KeyWrapAad = Encoding.UTF8.GetBytes("PhantomVault:KeyWrap:v1");
    private const int ExpectedKeyLength = 32;   // 256 bits
    private const int NonceLength = 12;         // 96 bits
    private const int TagLength = 16;           // 128 bits

    private readonly ICryptographyService _cryptoService;

    public KeyWrappingService(ICryptographyService cryptoService)
    {
        _cryptoService = cryptoService ?? throw new ArgumentNullException(nameof(cryptoService));
    }

    public (byte[] wrappedKey, byte[] nonce, byte[] tag) WrapKey(ReadOnlySpan<byte> masterKey, ReadOnlySpan<byte> kek)
    {
        if (masterKey.Length != ExpectedKeyLength)
            throw new ArgumentException($"Master key must be {ExpectedKeyLength} bytes.", nameof(masterKey));

        if (kek.Length != ExpectedKeyLength)
            throw new ArgumentException($"KEK must be {ExpectedKeyLength} bytes.", nameof(kek));

        byte[] nonce = _cryptoService.GenerateSecureRandomBytes(NonceLength);
        byte[] wrappedKey = _cryptoService.EncryptBytes(masterKey, kek, nonce, KeyWrapAad, out byte[] tag);

        return (wrappedKey, nonce, tag);
    }

    public byte[] UnwrapKey(ReadOnlySpan<byte> wrappedKey, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> tag, ReadOnlySpan<byte> kek)
    {
        if (wrappedKey.Length != ExpectedKeyLength)
            throw new ArgumentException($"Wrapped key must be {ExpectedKeyLength} bytes.", nameof(wrappedKey));

        if (kek.Length != ExpectedKeyLength)
            throw new ArgumentException($"KEK must be {ExpectedKeyLength} bytes.", nameof(kek));

        if (nonce.Length != NonceLength)
            throw new ArgumentException($"Nonce must be {NonceLength} bytes.", nameof(nonce));

        if (tag.Length != TagLength)
            throw new ArgumentException($"Tag must be {TagLength} bytes.", nameof(tag));

        try
        {
            return _cryptoService.DecryptBytes(wrappedKey, kek, nonce, tag, KeyWrapAad);
        }
        catch (CryptographicIntegrityException ex)
        {
            throw new InvalidCredentialsException("Failed to unwrap master key: Incorrect password or corrupted vault header.", ex);
        }
    }
}
