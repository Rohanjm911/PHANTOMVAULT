using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PhantomVault.Core.Exceptions;
using PhantomVault.Core.Interfaces;
using PhantomVault.Core.Models;

using System.Runtime.Versioning;

namespace PhantomVault.Steganography;

/// <summary>
/// Audio steganography engine embedding encrypted payloads into 16-bit PCM WAV audio samples.
/// </summary>
[SupportedOSPlatform("windows")]
public static class AudioStegoEngine
{
    public static async Task<string> EmbedAsync(
        string carrierPath, 
        string payloadPath, 
        string password, 
        string outputPath, 
        ICryptographyService crypto, 
        IKeyDerivationService kdf, 
        IProgress<double>? progress = null, 
        CancellationToken ct = default)
    {
        var carrierInfo = CapacityCalculator.AnalyzeCarrier(carrierPath);
        if (!carrierInfo.IsSuitable)
            throw new PhantomVaultException($"WAV carrier is not suitable: {carrierInfo.StatusMessage}");

        byte[] container = PayloadContainer.CreateContainer(payloadPath, password, crypto, kdf);
        if (container.Length > carrierInfo.UsableCapacityBytes + PayloadContainer.ContainerOverhead)
        {
            throw new CarrierCapacityExceededException(container.Length, carrierInfo.UsableCapacityBytes);
        }

        byte[] wavBytes = await File.ReadAllBytesAsync(carrierPath, ct).ConfigureAwait(false);
        int dataOffset = FindWavDataChunkOffset(wavBytes, out int dataLength);

        if (dataOffset < 0 || dataLength < container.Length * 8 * 2)
        {
            throw new VaultCorruptedException("WAV carrier data chunk insufficient for payload.");
        }

        int totalBitsToEmbed = container.Length * 8;
        int sampleOffset = dataOffset;

        // Iterate through 16-bit samples (2 bytes per sample, little-endian)
        for (int bitIndex = 0; bitIndex < totalBitsToEmbed; bitIndex++)
        {
            int bytePos = bitIndex / 8;
            int bitPos = 7 - (bitIndex % 8);
            byte bit = (byte)((container[bytePos] >> bitPos) & 1);

            short sample = BinaryPrimitives.ReadInt16LittleEndian(wavBytes.AsSpan(sampleOffset, 2));
            sample = (short)((sample & ~1) | bit);
            BinaryPrimitives.WriteInt16LittleEndian(wavBytes.AsSpan(sampleOffset, 2), sample);

            sampleOffset += 2;
        }

        string? outDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
        {
            Directory.CreateDirectory(outDir);
        }

        await File.WriteAllBytesAsync(outputPath, wavBytes, ct).ConfigureAwait(false);
        progress?.Report(1.0);
        return outputPath;
    }

    public static async Task<StegoPayloadInfo> ExtractAsync(
        string carrierPath, 
        string password, 
        string outputDirectory, 
        ICryptographyService crypto, 
        IKeyDerivationService kdf, 
        IProgress<double>? progress = null, 
        CancellationToken ct = default)
    {
        byte[] wavBytes = await File.ReadAllBytesAsync(carrierPath, ct).ConfigureAwait(false);
        int dataOffset = FindWavDataChunkOffset(wavBytes, out int dataLength);

        if (dataOffset < 0 || dataLength < PayloadContainer.HeaderSize * 8 * 2)
        {
            throw new VaultCorruptedException("WAV carrier contains no data chunk or is too small.");
        }

        // 1. Read Header (44 bytes = 352 bits = 352 samples)
        byte[] headerBuffer = new byte[PayloadContainer.HeaderSize];
        int sampleOffset = dataOffset;

        for (int bitIndex = 0; bitIndex < PayloadContainer.HeaderSize * 8; bitIndex++)
        {
            short sample = BinaryPrimitives.ReadInt16LittleEndian(wavBytes.AsSpan(sampleOffset, 2));
            byte bit = (byte)(sample & 1);

            int bytePos = bitIndex / 8;
            int bitPos = 7 - (bitIndex % 8);
            headerBuffer[bytePos] |= (byte)(bit << bitPos);

            sampleOffset += 2;
        }

        // Verify magic
        if (headerBuffer[0] != PayloadContainer.Magic[0] ||
            headerBuffer[1] != PayloadContainer.Magic[1] ||
            headerBuffer[2] != PayloadContainer.Magic[2] ||
            headerBuffer[3] != PayloadContainer.Magic[3])
        {
            throw new VaultCorruptedException("No valid PHANTOMVAULT container detected in this WAV audio carrier.");
        }

        int cipherLen = BinaryPrimitives.ReadInt32BigEndian(headerBuffer.AsSpan(40, 4));
        int totalContainerSize = PayloadContainer.HeaderSize + cipherLen + PayloadContainer.TagSize;

        if (totalContainerSize * 8 * 2 > dataLength)
        {
            throw new VaultCorruptedException("Declared payload size exceeds available WAV data samples.");
        }

        // 2. Read full container
        byte[] containerBytes = new byte[totalContainerSize];
        Array.Copy(headerBuffer, 0, containerBytes, 0, PayloadContainer.HeaderSize);

        sampleOffset = dataOffset + (PayloadContainer.HeaderSize * 8 * 2);
        for (int bitIndex = PayloadContainer.HeaderSize * 8; bitIndex < totalContainerSize * 8; bitIndex++)
        {
            short sample = BinaryPrimitives.ReadInt16LittleEndian(wavBytes.AsSpan(sampleOffset, 2));
            byte bit = (byte)(sample & 1);

            int bytePos = bitIndex / 8;
            int bitPos = 7 - (bitIndex % 8);
            containerBytes[bytePos] |= (byte)(bit << bitPos);

            sampleOffset += 2;
        }

        var (fileName, content, sha256Hex) = PayloadContainer.ExtractFromContainer(containerBytes, password, crypto, kdf);

        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        string outPath = Path.Combine(outputDirectory, fileName);
        await File.WriteAllBytesAsync(outPath, content, ct).ConfigureAwait(false);

        return new StegoPayloadInfo
        {
            OriginalFileName = fileName,
            PayloadSizeBytes = content.Length,
            Sha256Hash = sha256Hex,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static int FindWavDataChunkOffset(byte[] wav, out int dataLength)
    {
        dataLength = 0;
        if (wav.Length < 44) return -1;

        int pos = 12; // Skip "RIFF", size, "WAVE"
        while (pos < wav.Length - 8)
        {
            string chunkId = "" + (char)wav[pos] + (char)wav[pos + 1] + (char)wav[pos + 2] + (char)wav[pos + 3];
            int chunkSize = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(pos + 4, 4));

            if (chunkId == "data")
            {
                dataLength = chunkSize;
                return pos + 8;
            }

            pos += 8 + chunkSize;
        }

        return -1;
    }
}
