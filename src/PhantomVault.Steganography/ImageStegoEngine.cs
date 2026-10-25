using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using PhantomVault.Core.Exceptions;
using PhantomVault.Core.Interfaces;
using PhantomVault.Core.Models;

namespace PhantomVault.Steganography;

/// <summary>
/// Dedicated C# engine for PNG carrier LSB embedding and extraction.
/// Uses 24bpp RGB scanline iteration ensuring zero stride-padding contamination.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ImageStegoEngine
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
        if (outputPath.Equals(carrierPath, StringComparison.OrdinalIgnoreCase))
            throw new PhantomVaultException("Output path must be distinct from original carrier to preserve carrier integrity.");

        var carrierInfo = CapacityCalculator.AnalyzeCarrier(carrierPath);
        if (!carrierInfo.IsSuitable)
            throw new PhantomVaultException($"Carrier image is not suitable: {carrierInfo.StatusMessage}");

        // Build container
        byte[] container = PayloadContainer.CreateContainer(payloadPath, password, crypto, kdf);

        if (container.Length > carrierInfo.UsableCapacityBytes + PayloadContainer.ContainerOverhead)
        {
            throw new CarrierCapacityExceededException(container.Length, carrierInfo.UsableCapacityBytes);
        }

        progress?.Report(0.2);

        await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            using var originalBitmap = new Bitmap(carrierPath);
            int width = originalBitmap.Width;
            int height = originalBitmap.Height;

            // Use 24bpp RGB for lossless LSB storage without alpha conversion artifacts
            using var targetBitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(targetBitmap))
            {
                g.DrawImage(originalBitmap, 0, 0, width, height);
            }

            var rect = new Rectangle(0, 0, width, height);
            BitmapData bmpData = targetBitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

            try
            {
                int stride = bmpData.Stride;
                int byteCount = Math.Abs(stride) * height;
                byte[] rgbValues = new byte[byteCount];
                Marshal.Copy(bmpData.Scan0, rgbValues, 0, byteCount);

                int bitIndex = 0;
                int totalBitsToEmbed = container.Length * 8;

                // Row-by-row traversal ensuring stride padding is never touched
                for (int y = 0; y < height && bitIndex < totalBitsToEmbed; y++)
                {
                    int rowOffset = y * stride;
                    for (int x = 0; x < width && bitIndex < totalBitsToEmbed; x++)
                    {
                        int pixelOffset = rowOffset + (x * 3);
                        for (int c = 0; c < 3 && bitIndex < totalBitsToEmbed; c++)
                        {
                            int bytePos = bitIndex / 8;
                            int bitPos = 7 - (bitIndex % 8);
                            byte bit = (byte)((container[bytePos] >> bitPos) & 1);

                            rgbValues[pixelOffset + c] = (byte)((rgbValues[pixelOffset + c] & 0xFE) | bit);
                            bitIndex++;
                        }
                    }
                }

                Marshal.Copy(rgbValues, 0, bmpData.Scan0, byteCount);
            }
            finally
            {
                targetBitmap.UnlockBits(bmpData);
            }

            progress?.Report(0.7);

            string? outDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            targetBitmap.Save(outputPath, ImageFormat.Png);
        }, ct).ConfigureAwait(false);

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
        if (!File.Exists(carrierPath))
            throw new FileNotFoundException($"Carrier file not found: {carrierPath}");

        progress?.Report(0.1);

        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            using var carrierBitmap = new Bitmap(carrierPath);
            int width = carrierBitmap.Width;
            int height = carrierBitmap.Height;

            // Lock bits as 24bpp RGB
            using var rgbBitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(rgbBitmap))
            {
                g.DrawImage(carrierBitmap, 0, 0, width, height);
            }

            var rect = new Rectangle(0, 0, width, height);
            BitmapData bmpData = rgbBitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            byte[] containerBytes;
            try
            {
                int stride = bmpData.Stride;
                int byteCount = Math.Abs(stride) * height;
                byte[] rgbValues = new byte[byteCount];
                Marshal.Copy(bmpData.Scan0, rgbValues, 0, byteCount);

                // 1. First extract Header (44 bytes = 352 bits)
                byte[] headerBuffer = new byte[PayloadContainer.HeaderSize];
                int bitIndex = 0;

                for (int y = 0; y < height && bitIndex < PayloadContainer.HeaderSize * 8; y++)
                {
                    int rowOffset = y * stride;
                    for (int x = 0; x < width && bitIndex < PayloadContainer.HeaderSize * 8; x++)
                    {
                        int pixelOffset = rowOffset + (x * 3);
                        for (int c = 0; c < 3 && bitIndex < PayloadContainer.HeaderSize * 8; c++)
                        {
                            byte bit = (byte)(rgbValues[pixelOffset + c] & 1);
                            int bytePos = bitIndex / 8;
                            int bitPos = 7 - (bitIndex % 8);

                            headerBuffer[bytePos] |= (byte)(bit << bitPos);
                            bitIndex++;
                        }
                    }
                }

                // Verify magic bytes
                if (headerBuffer[0] != PayloadContainer.Magic[0] ||
                    headerBuffer[1] != PayloadContainer.Magic[1] ||
                    headerBuffer[2] != PayloadContainer.Magic[2] ||
                    headerBuffer[3] != PayloadContainer.Magic[3])
                {
                    throw new VaultCorruptedException("No valid PHANTOMVAULT steganographic container detected in this image.");
                }

                int cipherLen = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(headerBuffer.AsSpan(40, 4));
                if (cipherLen <= 0)
                {
                    throw new VaultCorruptedException("Extracted container header contains invalid payload length.");
                }

                int totalContainerSize = PayloadContainer.HeaderSize + cipherLen + PayloadContainer.TagSize;
                int totalBitsToRead = totalContainerSize * 8;

                if (totalBitsToRead > (width * height * 3))
                {
                    throw new VaultCorruptedException("Declared container size exceeds total carrier pixel capacity.");
                }

                // 2. Read full container
                containerBytes = new byte[totalContainerSize];
                Array.Copy(headerBuffer, 0, containerBytes, 0, PayloadContainer.HeaderSize);

                bitIndex = 0;
                for (int y = 0; y < height && bitIndex < totalBitsToRead; y++)
                {
                    int rowOffset = y * stride;
                    for (int x = 0; x < width && bitIndex < totalBitsToRead; x++)
                    {
                        int pixelOffset = rowOffset + (x * 3);
                        for (int c = 0; c < 3 && bitIndex < totalBitsToRead; c++)
                        {
                            if (bitIndex >= PayloadContainer.HeaderSize * 8)
                            {
                                byte bit = (byte)(rgbValues[pixelOffset + c] & 1);
                                int bytePos = bitIndex / 8;
                                int bitPos = 7 - (bitIndex % 8);

                                containerBytes[bytePos] |= (byte)(bit << bitPos);
                            }
                            bitIndex++;
                        }
                    }
                }
            }
            finally
            {
                rgbBitmap.UnlockBits(bmpData);
            }

            progress?.Report(0.6);

            // Decrypt and verify payload
            var (fileName, content, sha256Hex) = PayloadContainer.ExtractFromContainer(containerBytes, password, crypto, kdf);

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            string extractedFilePath = Path.Combine(outputDirectory, fileName);
            File.WriteAllBytes(extractedFilePath, content);

            progress?.Report(1.0);

            return new StegoPayloadInfo
            {
                OriginalFileName = fileName,
                PayloadSizeBytes = content.Length,
                Sha256Hash = sha256Hex,
                CreatedAtUtc = DateTime.UtcNow
            };
        }, ct).ConfigureAwait(false);
    }
}
