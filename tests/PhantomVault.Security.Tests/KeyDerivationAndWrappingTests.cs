using System;
using System.Text;
using PhantomVault.Core.Enums;
using PhantomVault.Core.Exceptions;
using PhantomVault.Security.Cryptography;
using PhantomVault.Security.KeyManagement;
using PhantomVault.Security.SecureBuffering;
using Xunit;

namespace PhantomVault.Security.Tests;

public class KeyDerivationAndWrappingTests
{
    private readonly KeyDerivationService _kdf = new();
    private readonly AesGcmCryptoProvider _crypto = new();
    private readonly KeyWrappingService _keyWrapper;

    public KeyDerivationAndWrappingTests()
    {
        _keyWrapper = new KeyWrappingService(_crypto);
    }

    [Fact]
    public void Argon2id_SameInputs_ProduceIdenticalKey()
    {
        byte[] salt = _crypto.GenerateSecureRandomBytes(16);
        string password = "Correct-Horse-Battery-Staple!";

        // Use 4MB and 2 iterations for fast unit test execution
        byte[] key1 = _kdf.DeriveKey(password, salt, KdfAlgorithm.Argon2id, iterations: 2, memoryKiB: 4096, parallelism: 2);
        byte[] key2 = _kdf.DeriveKey(password, salt, KdfAlgorithm.Argon2id, iterations: 2, memoryKiB: 4096, parallelism: 2);

        Assert.Equal(32, key1.Length);
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void Argon2id_DifferentPasswords_ProduceDifferentKeys()
    {
        byte[] salt = _crypto.GenerateSecureRandomBytes(16);

        byte[] key1 = _kdf.DeriveKey("PasswordA", salt, KdfAlgorithm.Argon2id, iterations: 2, memoryKiB: 2048, parallelism: 1);
        byte[] key2 = _kdf.DeriveKey("PasswordB", salt, KdfAlgorithm.Argon2id, iterations: 2, memoryKiB: 2048, parallelism: 1);

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void Argon2id_InvalidParameters_ThrowsException()
    {
        byte[] salt = _crypto.GenerateSecureRandomBytes(16);

        // Memory < 1024 KiB
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _kdf.DeriveKey("TestPass", salt, KdfAlgorithm.Argon2id, iterations: 1, memoryKiB: 512, parallelism: 1));

        // Short salt (< 16 bytes)
        Assert.Throws<ArgumentException>(() =>
            _kdf.DeriveKey("TestPass", new byte[8], KdfAlgorithm.Argon2id, iterations: 1, memoryKiB: 1024, parallelism: 1));
    }

    [Fact]
    public void Pbkdf2_Derivation_Succeeds()
    {
        byte[] salt = _crypto.GenerateSecureRandomBytes(16);
        string password = "Pbkdf2TestPassword";

        byte[] key = _kdf.DeriveKey(password, salt, KdfAlgorithm.Pbkdf2HmacSha256, iterations: 100_000);
        Assert.Equal(32, key.Length);
    }

    [Fact]
    public void KeyWrapping_RoundTrip_SuccessfullyRecoversMasterKey()
    {
        byte[] masterKey = _crypto.GenerateSecureRandomBytes(32);
        byte[] kek = _crypto.GenerateSecureRandomBytes(32);

        var (wrapped, nonce, tag) = _keyWrapper.WrapKey(masterKey, kek);

        Assert.Equal(32, wrapped.Length);
        Assert.Equal(12, nonce.Length);
        Assert.Equal(16, tag.Length);

        byte[] unwrapped = _keyWrapper.UnwrapKey(wrapped, nonce, tag, kek);
        Assert.Equal(masterKey, unwrapped);
    }

    [Fact]
    public void KeyWrapping_WrongKek_ThrowsInvalidCredentialsException()
    {
        byte[] masterKey = _crypto.GenerateSecureRandomBytes(32);
        byte[] kekCorrect = _crypto.GenerateSecureRandomBytes(32);
        byte[] kekWrong = _crypto.GenerateSecureRandomBytes(32);

        var (wrapped, nonce, tag) = _keyWrapper.WrapKey(masterKey, kekCorrect);

        Assert.Throws<InvalidCredentialsException>(() =>
            _keyWrapper.UnwrapKey(wrapped, nonce, tag, kekWrong));
    }

    [Fact]
    public void SecureBuffer_ZeroizesDataOnDispose()
    {
        byte[] testData = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0x04 };
        byte[] dangerousRef;

        using (var secBuf = new SecureBuffer(testData))
        {
            dangerousRef = secBuf.DangerousGetBuffer();
            Assert.Equal(0xDE, dangerousRef[0]);
        }

        // After disposal, memory should be zeroized
        Assert.All(dangerousRef, b => Assert.Equal(0, b));
    }
}
