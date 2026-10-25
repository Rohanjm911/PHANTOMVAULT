using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using PhantomVault.Core.Enums;
using PhantomVault.Core.Exceptions;
using PhantomVault.Core.Interfaces;

namespace PhantomVault.Steganography;

/// <summary>
/// Encapsulates the binary container format (PVST) for steganographic payloads.
/// </summary>
public static class PayloadContainer
{
    public static readonly byte[] Magic = new byte[] { 0x50, 0x56, 0x53, 0x54 }; // "PVST"
    public const ushort CurrentVersion = 1;

    // Header size:
    // Magic (4B) + Version (2B) + Kdf (2B) + Salt (16B) + Iterations (4B) + 
    // Nonce (12B) + CiphertextLength (4B) = 44 bytes
    // Plus Tag (16B) = 60 bytes minimum container overhead
    public const int HeaderSize = 44;
    public const int TagSize = 16;
    public const int ContainerOverhead = HeaderSize + TagSize;

    private static readonly byte[] ContainerAad = Encoding.UTF8.GetBytes("PhantomVault:StegoContainer:v1");

    public static byte[] CreateContainer(
        string payloadFilePath, 
        string password, 
        ICryptographyService crypto, 
        IKeyDerivationService kdf)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        if (!File.Exists(payloadFilePath))
            throw new FileNotFoundException($"Payload file not found: {payloadFilePath}");

        string fileName = Path.GetFileName(payloadFilePath);
        byte[] fileNameBytes = Encoding.UTF8.GetBytes(fileName);
        byte[] fileBytes = File.ReadAllBytes(payloadFilePath);

        // Plaintext structure:
        // FileNameLen (2B) + FileNameBytes + ContentLen (4B) + ContentBytes + SHA-256 (32B)
        byte[] sha256 = SHA256.HashData(fileBytes);
        int plaintextLen = 2 + fileNameBytes.Length + 4 + fileBytes.Length + 32;
        byte[] plaintext = new byte[plaintextLen];
        int offset = 0;

        BinaryPrimitives.WriteUInt16BigEndian(plaintext.AsSpan(offset, 2), (ushort)fileNameBytes.Length);
        offset += 2;
        fileNameBytes.CopyTo(plaintext.AsSpan(offset));
        offset += fileNameBytes.Length;

        BinaryPrimitives.WriteInt32BigEndian(plaintext.AsSpan(offset, 4), fileBytes.Length);
        offset += 4;
        fileBytes.CopyTo(plaintext.AsSpan(offset));
        offset += fileBytes.Length;

        sha256.CopyTo(plaintext.AsSpan(offset, 32));

        // Derive KEK for payload
        byte[] salt = crypto.GenerateSecureRandomBytes(16);
        uint iterations = 2; // Fast yet secure for stego payloads
        byte[] key = kdf.DeriveKey(password, salt, KdfAlgorithm.Argon2id, iterations: iterations, memoryKiB: 4096, parallelism: 2);

        // Encrypt with AES-256-GCM
        byte[] nonce = crypto.GenerateSecureRandomBytes(12);
        byte[] ciphertext = crypto.EncryptBytes(plaintext, key, nonce, ContainerAad, out byte[] tag);

        // Assemble Container: Header (44B) + Ciphertext (NB) + Tag (16B)
        byte[] container = new byte[HeaderSize + ciphertext.Length + TagSize];
        offset = 0;

        Magic.CopyTo(container.AsSpan(offset, 4));
        offset += 4;

        BinaryPrimitives.WriteUInt16BigEndian(container.AsSpan(offset, 2), CurrentVersion);
        offset += 2;

        BinaryPrimitives.WriteUInt16BigEndian(container.AsSpan(offset, 2), (ushort)KdfAlgorithm.Argon2id);
        offset += 2;

        salt.CopyTo(container.AsSpan(offset, 16));
        offset += 16;

        BinaryPrimitives.WriteUInt32BigEndian(container.AsSpan(offset, 4), iterations);
        offset += 4;

        nonce.CopyTo(container.AsSpan(offset, 12));
        offset += 12;

        BinaryPrimitives.WriteInt32BigEndian(container.AsSpan(offset, 4), ciphertext.Length);
        offset += 4;

        ciphertext.CopyTo(container.AsSpan(offset));
        offset += ciphertext.Length;

        tag.CopyTo(container.AsSpan(offset, 16));

        return container;
    }

    public static (string fileName, byte[] content, string sha256Hex) ExtractFromContainer(
        ReadOnlySpan<byte> containerData, 
        string password, 
        ICryptographyService crypto, 
        IKeyDerivationService kdf)
    {
        if (containerData.Length < ContainerOverhead)
            throw new VaultCorruptedException("Extracted bitstream is too small to contain a valid container.");

        int offset = 0;
        if (!containerData.Slice(offset, 4).SequenceEqual(Magic))
            throw new VaultCorruptedException("Carrier does not contain a valid PHANTOMVAULT container (Magic mismatch).");
        offset += 4;

        ushort version = BinaryPrimitives.ReadUInt16BigEndian(containerData.Slice(offset, 2));
        offset += 2;
        if (version != CurrentVersion)
            throw new UnsupportedFormatException($"Unsupported stego container version: {version}");

        ushort kdfId = BinaryPrimitives.ReadUInt16BigEndian(containerData.Slice(offset, 2));
        offset += 2;

        byte[] salt = containerData.Slice(offset, 16).ToArray();
        offset += 16;

        uint iterations = BinaryPrimitives.ReadUInt32BigEndian(containerData.Slice(offset, 4));
        offset += 4;

        byte[] nonce = containerData.Slice(offset, 12).ToArray();
        offset += 12;

        int cipherLen = BinaryPrimitives.ReadInt32BigEndian(containerData.Slice(offset, 4));
        offset += 4;

        if (cipherLen <= 0 || offset + cipherLen + TagSize > containerData.Length)
            throw new VaultCorruptedException("Invalid payload length declared in stego container header.");

        byte[] ciphertext = containerData.Slice(offset, cipherLen).ToArray();
        offset += cipherLen;

        byte[] tag = containerData.Slice(offset, TagSize).ToArray();

        // Derive key
        byte[] key = kdf.DeriveKey(password, salt, (KdfAlgorithm)kdfId, iterations: iterations, memoryKiB: 4096, parallelism: 2);

        // Decrypt
        byte[] plaintext;
        try
        {
            plaintext = crypto.DecryptBytes(ciphertext, key, nonce, tag, ContainerAad);
        }
        catch (CryptographicIntegrityException ex)
        {
            throw new InvalidCredentialsException("Failed to decrypt steganographic payload. Password incorrect or carrier modified.", ex);
        }

        // Unpack Plaintext
        offset = 0;
        ushort nameLen = BinaryPrimitives.ReadUInt16BigEndian(plaintext.AsSpan(offset, 2));
        offset += 2;

        string fileName = Encoding.UTF8.GetString(plaintext, offset, nameLen);
        offset += nameLen;

        int contentLen = BinaryPrimitives.ReadInt32BigEndian(plaintext.AsSpan(offset, 4));
        offset += 4;

        byte[] content = new byte[contentLen];
        Array.Copy(plaintext, offset, content, 0, contentLen);
        offset += contentLen;

        byte[] expectedHash = new byte[32];
        Array.Copy(plaintext, offset, expectedHash, 0, 32);

        byte[] actualHash = SHA256.HashData(content);
        if (!actualHash.AsSpan().SequenceEqual(expectedHash))
        {
            throw new CryptographicIntegrityException("Post-extraction SHA-256 verification failed on payload.");
        }

        return (fileName, content, Convert.ToHexString(actualHash));
    }
}
