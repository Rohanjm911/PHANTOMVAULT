using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using PhantomVault.Core.Exceptions;
using PhantomVault.Security.Cryptography;
using Xunit;

namespace PhantomVault.Security.Tests;

public class AesGcmCryptoProviderTests
{
    private readonly AesGcmCryptoProvider _crypto = new();

    [Fact]
    public void EncryptDecrypt_RoundTrip_MatchesPlaintext()
    {
        byte[] key = _crypto.GenerateSecureRandomBytes(32);
        byte[] nonce = _crypto.GenerateSecureRandomBytes(12);
        byte[] aad = Encoding.UTF8.GetBytes("vault-test-aad");
        byte[] plaintext = Encoding.UTF8.GetBytes("The quick brown fox jumps over the lazy dog.");

        byte[] ciphertext = _crypto.EncryptBytes(plaintext, key, nonce, aad, out byte[] tag);
        Assert.NotNull(ciphertext);
        Assert.Equal(plaintext.Length, ciphertext.Length);
        Assert.NotNull(tag);
        Assert.Equal(16, tag.Length);

        byte[] decrypted = _crypto.DecryptBytes(ciphertext, key, nonce, tag, aad);
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ThrowsCryptographicIntegrityException()
    {
        byte[] key = _crypto.GenerateSecureRandomBytes(32);
        byte[] nonce = _crypto.GenerateSecureRandomBytes(12);
        byte[] aad = Encoding.UTF8.GetBytes("test-aad");
        byte[] plaintext = Encoding.UTF8.GetBytes("Sensitive confidential vault payload");

        byte[] ciphertext = _crypto.EncryptBytes(plaintext, key, nonce, aad, out byte[] tag);

        // Tamper with a single bit in the ciphertext
        ciphertext[0] ^= 0x01;

        Assert.Throws<CryptographicIntegrityException>(() =>
            _crypto.DecryptBytes(ciphertext, key, nonce, tag, aad));
    }

    [Fact]
    public void Decrypt_TamperedTag_ThrowsCryptographicIntegrityException()
    {
        byte[] key = _crypto.GenerateSecureRandomBytes(32);
        byte[] nonce = _crypto.GenerateSecureRandomBytes(12);
        byte[] aad = Encoding.UTF8.GetBytes("test-aad");
        byte[] plaintext = Encoding.UTF8.GetBytes("Integrity check test");

        byte[] ciphertext = _crypto.EncryptBytes(plaintext, key, nonce, aad, out byte[] tag);

        // Tamper with auth tag
        tag[15] ^= 0xFF;

        Assert.Throws<CryptographicIntegrityException>(() =>
            _crypto.DecryptBytes(ciphertext, key, nonce, tag, aad));
    }

    [Fact]
    public void Decrypt_TamperedAAD_ThrowsCryptographicIntegrityException()
    {
        byte[] key = _crypto.GenerateSecureRandomBytes(32);
        byte[] nonce = _crypto.GenerateSecureRandomBytes(12);
        byte[] aad = Encoding.UTF8.GetBytes("original-aad");
        byte[] alteredAad = Encoding.UTF8.GetBytes("altered-aad");
        byte[] plaintext = Encoding.UTF8.GetBytes("Integrity test");

        byte[] ciphertext = _crypto.EncryptBytes(plaintext, key, nonce, aad, out byte[] tag);

        Assert.Throws<CryptographicIntegrityException>(() =>
            _crypto.DecryptBytes(ciphertext, key, nonce, tag, alteredAad));
    }

    [Fact]
    public async Task EncryptStreamChunked_SmallStream_RoundTripSucceeds()
    {
        byte[] key = _crypto.GenerateSecureRandomBytes(32);
        Guid fileId = Guid.NewGuid();
        byte[] original = Encoding.UTF8.GetBytes("Short payload content for stream testing.");

        using var inStream = new MemoryStream(original);
        using var encStream = new MemoryStream();

        await _crypto.EncryptStreamChunkedAsync(inStream, encStream, key, fileId);

        encStream.Position = 0;
        using var decStream = new MemoryStream();
        await _crypto.DecryptStreamChunkedAsync(encStream, decStream, key, fileId);

        Assert.Equal(original, decStream.ToArray());
    }

    [Fact]
    public async Task EncryptStreamChunked_MultiChunkStream_RoundTripSucceeds()
    {
        byte[] key = _crypto.GenerateSecureRandomBytes(32);
        Guid fileId = Guid.NewGuid();

        // 180 KiB payload forces 3 chunks (64 KiB + 64 KiB + 52 KiB)
        byte[] original = new byte[180 * 1024];
        RandomNumberGenerator.Fill(original);

        using var inStream = new MemoryStream(original);
        using var encStream = new MemoryStream();

        await _crypto.EncryptStreamChunkedAsync(inStream, encStream, key, fileId);

        encStream.Position = 0;
        using var decStream = new MemoryStream();
        await _crypto.DecryptStreamChunkedAsync(encStream, decStream, key, fileId);

        Assert.Equal(original, decStream.ToArray());
    }

    [Fact]
    public async Task DecryptStreamChunked_TruncatedStream_ThrowsCryptographicIntegrityException()
    {
        byte[] key = _crypto.GenerateSecureRandomBytes(32);
        Guid fileId = Guid.NewGuid();

        byte[] original = new byte[150 * 1024];
        RandomNumberGenerator.Fill(original);

        using var inStream = new MemoryStream(original);
        using var encStream = new MemoryStream();
        await _crypto.EncryptStreamChunkedAsync(inStream, encStream, key, fileId);

        // Truncate the stream by 100 bytes
        byte[] encryptedBytes = encStream.ToArray();
        byte[] truncatedBytes = new byte[encryptedBytes.Length - 100];
        Array.Copy(encryptedBytes, truncatedBytes, truncatedBytes.Length);

        using var truncatedStream = new MemoryStream(truncatedBytes);
        using var decStream = new MemoryStream();

        await Assert.ThrowsAnyAsync<PhantomVaultException>(async () =>
            await _crypto.DecryptStreamChunkedAsync(truncatedStream, decStream, key, fileId));
    }
}
