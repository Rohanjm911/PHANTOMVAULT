using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using PhantomVault.Core.Exceptions;
using PhantomVault.Core.Interfaces;

namespace PhantomVault.Security.Cryptography;

/// <summary>
/// Authenticated encryption service implementing AES-256-GCM and chunked stream processing.
/// </summary>
public sealed class AesGcmCryptoProvider : ICryptographyService
{
    public const int KeySizeBytes = 32;       // 256 bits
    public const int NonceSizeBytes = 12;     // 96 bits
    public const int TagSizeBytes = 16;       // 128 bits
    public const int DefaultChunkSize = 65536; // 64 KiB

    private static readonly byte[] ChunkHeaderMagic = new byte[] { 0x50, 0x56, 0x43, 0x46 }; // "PVCF"
    private const ushort ChunkFormatVersion = 1;

    public byte[] GenerateSecureRandomBytes(int length)
    {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be greater than zero.");

        byte[] bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        return bytes;
    }

    public byte[] EncryptBytes(
        ReadOnlySpan<byte> plaintext, 
        ReadOnlySpan<byte> key, 
        ReadOnlySpan<byte> nonce, 
        ReadOnlySpan<byte> associatedData, 
        out byte[] tag)
    {
        if (key.Length != KeySizeBytes)
            throw new ArgumentException($"Key must be {KeySizeBytes} bytes ({KeySizeBytes * 8} bits).", nameof(key));

        if (nonce.Length != NonceSizeBytes)
            throw new ArgumentException($"Nonce must be {NonceSizeBytes} bytes ({NonceSizeBytes * 8} bits).", nameof(nonce));

        byte[] ciphertext = new byte[plaintext.Length];
        tag = new byte[TagSizeBytes];

        using var aesGcm = new AesGcm(key, TagSizeBytes);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

        return ciphertext;
    }

    public byte[] DecryptBytes(
        ReadOnlySpan<byte> ciphertext, 
        ReadOnlySpan<byte> key, 
        ReadOnlySpan<byte> nonce, 
        ReadOnlySpan<byte> tag, 
        ReadOnlySpan<byte> associatedData)
    {
        if (key.Length != KeySizeBytes)
            throw new ArgumentException($"Key must be {KeySizeBytes} bytes.", nameof(key));

        if (nonce.Length != NonceSizeBytes)
            throw new ArgumentException($"Nonce must be {NonceSizeBytes} bytes.", nameof(nonce));

        if (tag.Length != TagSizeBytes)
            throw new ArgumentException($"Tag must be {TagSizeBytes} bytes.", nameof(tag));

        byte[] plaintext = new byte[ciphertext.Length];

        try
        {
            using var aesGcm = new AesGcm(key, TagSizeBytes);
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
            return plaintext;
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicIntegrityException("AES-GCM decryption failed: ciphertext or authentication tag has been tampered with or key is invalid.", ex);
        }
    }

    public async Task EncryptStreamChunkedAsync(
        Stream inputStream, 
        Stream outputStream, 
        byte[] key, 
        Guid fileId, 
        IProgress<double>? progress = null, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputStream);
        ArgumentNullException.ThrowIfNull(outputStream);
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length != KeySizeBytes)
            throw new ArgumentException($"Key must be {KeySizeBytes} bytes.", nameof(key));

        long totalInputLength = inputStream.CanSeek ? inputStream.Length : -1;
        long bytesProcessed = 0;

        // 1. Write Header: Magic "PVCF" (4B) + Version (2B) + Reserved (2B)
        await outputStream.WriteAsync(ChunkHeaderMagic, cancellationToken).ConfigureAwait(false);
        byte[] versionBytes = new byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(versionBytes.AsSpan(0, 2), ChunkFormatVersion);
        BinaryPrimitives.WriteUInt16BigEndian(versionBytes.AsSpan(2, 2), 0); // reserved
        await outputStream.WriteAsync(versionBytes, cancellationToken).ConfigureAwait(false);

        // 2. Generate 64-bit random IV prefix for this file
        byte[] fileIvPrefix = GenerateSecureRandomBytes(8);
        byte[] fileIdBytes = fileId.ToByteArray();

        using var aesGcm = new AesGcm(key, TagSizeBytes);
        byte[] readBuffer = new byte[DefaultChunkSize];
        byte[] nonceBuffer = new byte[NonceSizeBytes];
        byte[] aadBuffer = new byte[16 + 4 + 1]; // FileId (16B) + ChunkIndex (4B) + IsLast (1B)
        byte[] lenBuffer = new byte[4];
        byte[] tagBuffer = new byte[TagSizeBytes];

        Array.Copy(fileIvPrefix, 0, nonceBuffer, 0, 8);
        Array.Copy(fileIdBytes, 0, aadBuffer, 0, 16);

        uint chunkIndex = 0;
        bool hasNextChunk = true;

        // Handle empty files cleanly
        int bytesRead = await inputStream.ReadAsync(readBuffer.AsMemory(0, DefaultChunkSize), cancellationToken).ConfigureAwait(false);
        
        while (hasNextChunk)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int currentChunkLength = bytesRead;
            // Check if there is another chunk after this
            byte[] nextBuffer = new byte[DefaultChunkSize];
            int nextBytesRead = await inputStream.ReadAsync(nextBuffer.AsMemory(0, DefaultChunkSize), cancellationToken).ConfigureAwait(false);
            byte isLastChunk = (nextBytesRead == 0) ? (byte)1 : (byte)0;

            // Form chunk nonce: IV prefix (8 bytes) + chunkIndex (4 bytes big-endian)
            BinaryPrimitives.WriteUInt32BigEndian(nonceBuffer.AsSpan(8, 4), chunkIndex);

            // Form AAD: FileId (16) + chunkIndex (4) + isLastChunk (1)
            BinaryPrimitives.WriteUInt32BigEndian(aadBuffer.AsSpan(16, 4), chunkIndex);
            aadBuffer[20] = isLastChunk;

            // Encrypt current chunk
            byte[] chunkCiphertext = new byte[currentChunkLength];
            aesGcm.Encrypt(
                nonceBuffer.AsSpan(), 
                readBuffer.AsSpan(0, currentChunkLength), 
                chunkCiphertext.AsSpan(), 
                tagBuffer.AsSpan(), 
                aadBuffer.AsSpan());

            // Write chunk record: Nonce (12B) + CiphertextLength (4B) + Ciphertext (NB) + Tag (16B)
            await outputStream.WriteAsync(nonceBuffer, cancellationToken).ConfigureAwait(false);
            BinaryPrimitives.WriteInt32BigEndian(lenBuffer.AsSpan(), currentChunkLength);
            await outputStream.WriteAsync(lenBuffer, cancellationToken).ConfigureAwait(false);
            await outputStream.WriteAsync(chunkCiphertext, cancellationToken).ConfigureAwait(false);
            await outputStream.WriteAsync(tagBuffer, cancellationToken).ConfigureAwait(false);

            bytesProcessed += currentChunkLength;
            if (totalInputLength > 0 && progress != null)
            {
                progress.Report(Math.Min(1.0, (double)bytesProcessed / totalInputLength));
            }

            if (isLastChunk == 1)
            {
                hasNextChunk = false;
            }
            else
            {
                readBuffer = nextBuffer;
                bytesRead = nextBytesRead;
                chunkIndex++;
            }
        }

        await outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DecryptStreamChunkedAsync(
        Stream inputStream, 
        Stream outputStream, 
        byte[] key, 
        Guid fileId, 
        IProgress<double>? progress = null, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputStream);
        ArgumentNullException.ThrowIfNull(outputStream);
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length != KeySizeBytes)
            throw new ArgumentException($"Key must be {KeySizeBytes} bytes.", nameof(key));

        long totalInputLength = inputStream.CanSeek ? inputStream.Length : -1;
        long bytesProcessed = 0;

        // 1. Read Header
        byte[] magic = new byte[4];
        await ReadExactAsync(inputStream, magic, cancellationToken).ConfigureAwait(false);
        bytesProcessed += 4;

        if (!magic.AsSpan().SequenceEqual(ChunkHeaderMagic))
            throw new VaultCorruptedException("Invalid chunked file header: magic bytes mismatch.");

        byte[] versionBytes = new byte[4];
        await ReadExactAsync(inputStream, versionBytes, cancellationToken).ConfigureAwait(false);
        bytesProcessed += 4;

        ushort version = BinaryPrimitives.ReadUInt16BigEndian(versionBytes.AsSpan(0, 2));
        if (version != ChunkFormatVersion)
            throw new UnsupportedFormatException($"Unsupported chunk format version: {version}. Expected: {ChunkFormatVersion}");

        byte[] fileIdBytes = fileId.ToByteArray();
        using var aesGcm = new AesGcm(key, TagSizeBytes);

        byte[] nonceBuffer = new byte[NonceSizeBytes];
        byte[] lenBuffer = new byte[4];
        byte[] tagBuffer = new byte[TagSizeBytes];
        byte[] aadBuffer = new byte[16 + 4 + 1];

        Array.Copy(fileIdBytes, 0, aadBuffer, 0, 16);

        uint expectedChunkIndex = 0;
        bool isComplete = false;

        while (!isComplete)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Read Nonce (12B)
            int read = await inputStream.ReadAsync(nonceBuffer.AsMemory(0, NonceSizeBytes), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                // Unexpected EOF before IsLastChunk was processed
                throw new CryptographicIntegrityException("Chunked file was truncated before the final authenticated chunk.");
            }
            if (read < NonceSizeBytes)
            {
                await ReadExactAsync(inputStream, nonceBuffer.AsMemory(read, NonceSizeBytes - read), cancellationToken).ConfigureAwait(false);
            }
            bytesProcessed += NonceSizeBytes;

            // Verify sequential chunk counter in nonce
            uint chunkIndexInNonce = BinaryPrimitives.ReadUInt32BigEndian(nonceBuffer.AsSpan(8, 4));
            if (chunkIndexInNonce != expectedChunkIndex)
            {
                throw new CryptographicIntegrityException($"Chunk sequence violated. Expected chunk {expectedChunkIndex}, but found {chunkIndexInNonce}. Possible chunk reordering attack.");
            }

            // Read Ciphertext Length (4B)
            await ReadExactAsync(inputStream, lenBuffer, cancellationToken).ConfigureAwait(false);
            bytesProcessed += 4;
            int cipherLength = BinaryPrimitives.ReadInt32BigEndian(lenBuffer.AsSpan());

            if (cipherLength < 0 || cipherLength > DefaultChunkSize * 2)
            {
                throw new VaultCorruptedException($"Illegal chunk length: {cipherLength} bytes.");
            }

            // Read Ciphertext
            byte[] ciphertext = new byte[cipherLength];
            if (cipherLength > 0)
            {
                await ReadExactAsync(inputStream, ciphertext, cancellationToken).ConfigureAwait(false);
                bytesProcessed += cipherLength;
            }

            // Read Tag (16B)
            await ReadExactAsync(inputStream, tagBuffer, cancellationToken).ConfigureAwait(false);
            bytesProcessed += TagSizeBytes;

            // Check if this is the final chunk by testing authentication with isLastChunk = 1 then 0, or by checking remaining stream
            // In our format: we test isLastChunk = 1 if end of stream reached, or verify AAD
            // Let's attempt decrypt with isLast = 0, and if stream is at end or tag mismatch, test with isLast = 1
            byte[] plaintext = new byte[cipherLength];
            bool decrypted = false;

            BinaryPrimitives.WriteUInt32BigEndian(aadBuffer.AsSpan(16, 4), expectedChunkIndex);

            // Check if end of stream is reached
            bool atStreamEnd = inputStream.CanSeek && (inputStream.Position == inputStream.Length);

            if (atStreamEnd)
            {
                // Try IsLast = 1 first
                aadBuffer[20] = 1;
                try
                {
                    aesGcm.Decrypt(nonceBuffer.AsSpan(), ciphertext.AsSpan(), tagBuffer.AsSpan(), plaintext.AsSpan(), aadBuffer.AsSpan());
                    decrypted = true;
                    isComplete = true;
                }
                catch (CryptographicException)
                {
                    // Fall back to trying 0 in case stream is not at end
                    aadBuffer[20] = 0;
                    try
                    {
                        aesGcm.Decrypt(nonceBuffer.AsSpan(), ciphertext.AsSpan(), tagBuffer.AsSpan(), plaintext.AsSpan(), aadBuffer.AsSpan());
                        decrypted = true;
                    }
                    catch (CryptographicException)
                    {
                        throw new CryptographicIntegrityException($"Authentication failed on chunk {expectedChunkIndex}. The file has been tampered with.");
                    }
                }
            }
            else
            {
                // Try IsLast = 0 first
                aadBuffer[20] = 0;
                try
                {
                    aesGcm.Decrypt(nonceBuffer.AsSpan(), ciphertext.AsSpan(), tagBuffer.AsSpan(), plaintext.AsSpan(), aadBuffer.AsSpan());
                    decrypted = true;
                }
                catch (CryptographicException)
                {
                    // Maybe it is the last chunk
                    aadBuffer[20] = 1;
                    try
                    {
                        aesGcm.Decrypt(nonceBuffer.AsSpan(), ciphertext.AsSpan(), tagBuffer.AsSpan(), plaintext.AsSpan(), aadBuffer.AsSpan());
                        decrypted = true;
                        isComplete = true;
                    }
                    catch (CryptographicException)
                    {
                        throw new CryptographicIntegrityException($"Authentication failed on chunk {expectedChunkIndex}. Ciphertext or metadata altered.");
                    }
                }
            }

            if (!decrypted)
            {
                throw new CryptographicIntegrityException($"Failed to authenticate chunk {expectedChunkIndex}.");
            }

            // Write decrypted plaintext
            if (cipherLength > 0)
            {
                await outputStream.WriteAsync(plaintext, cancellationToken).ConfigureAwait(false);
            }

            if (totalInputLength > 0 && progress != null)
            {
                progress.Report(Math.Min(1.0, (double)bytesProcessed / totalInputLength));
            }

            expectedChunkIndex++;
        }

        await outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ReadExactAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.Slice(totalRead), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new CryptographicIntegrityException($"Unexpected end of stream. Encrypted stream was truncated or corrupted. Expected {buffer.Length} bytes, read {totalRead} bytes.");
            }
            totalRead += read;
        }
    }
}
