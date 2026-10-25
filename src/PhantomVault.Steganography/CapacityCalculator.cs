using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.Versioning;
using PhantomVault.Core.Enums;
using PhantomVault.Core.Exceptions;
using PhantomVault.Core.Models;

namespace PhantomVault.Steganography;

/// <summary>
/// Calculates carrier storage capacity and validates payload fit.
/// </summary>
[SupportedOSPlatform("windows")]
public static class CapacityCalculator
{
    public static StegoCarrierInfo AnalyzeCarrier(string carrierFilePath)
    {
        if (!File.Exists(carrierFilePath))
            throw new FileNotFoundException($"Carrier file not found: {carrierFilePath}");

        string ext = Path.GetExtension(carrierFilePath).ToLowerInvariant();
        if (ext == ".png")
        {
            return AnalyzePng(carrierFilePath);
        }
        else if (ext == ".wav")
        {
            return AnalyzeWav(carrierFilePath);
        }
        else
        {
            return new StegoCarrierInfo
            {
                FilePath = carrierFilePath,
                IsSuitable = false,
                StatusMessage = $"Unsupported carrier extension '{ext}'. Only .png and .wav are supported."
            };
        }
    }

    private static StegoCarrierInfo AnalyzePng(string filePath)
    {
        try
        {
            using var image = new Bitmap(filePath);
            int width = image.Width;
            int height = image.Height;
            int channels = Image.GetPixelFormatSize(image.PixelFormat) / 8;

            if (channels < 3)
            {
                return new StegoCarrierInfo
                {
                    FilePath = filePath,
                    CarrierType = CarrierType.PngImage,
                    Width = width,
                    Height = height,
                    Channels = channels,
                    IsSuitable = false,
                    StatusMessage = "Monochrome or indexed palette images are not supported for high-security RGB LSB embedding."
                };
            }

            // We embed in RGB (3 color channels), leaving Alpha untouched
            long usableBits = (long)width * height * 3;
            long totalCapacityBytes = usableBits / 8;
            long usableCapacityBytes = Math.Max(0, totalCapacityBytes - PayloadContainer.ContainerOverhead);

            return new StegoCarrierInfo
            {
                FilePath = filePath,
                CarrierType = CarrierType.PngImage,
                Width = width,
                Height = height,
                Channels = channels,
                TotalCarrierSizeBytes = new FileInfo(filePath).Length,
                UsableCapacityBytes = usableCapacityBytes,
                IsSuitable = usableCapacityBytes > 100,
                StatusMessage = usableCapacityBytes > 100 ? "Suitable for PNG steganography." : "Image resolution too low for payload embedding."
            };
        }
        catch (Exception ex)
        {
            return new StegoCarrierInfo
            {
                FilePath = filePath,
                CarrierType = CarrierType.PngImage,
                IsSuitable = false,
                StatusMessage = $"Failed to parse PNG carrier: {ex.Message}"
            };
        }
    }

    private static StegoCarrierInfo AnalyzeWav(string filePath)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(fs);

            string riff = new string(reader.ReadChars(4));
            if (riff != "RIFF")
                return new StegoCarrierInfo { FilePath = filePath, IsSuitable = false, StatusMessage = "Not a valid RIFF file." };

            reader.ReadInt32(); // riff size
            string wave = new string(reader.ReadChars(4));
            if (wave != "WAVE")
                return new StegoCarrierInfo { FilePath = filePath, IsSuitable = false, StatusMessage = "Not a valid WAVE file." };

            // Find data chunk
            int audioDataSize = 0;
            short channels = 0;
            int sampleRate = 0;
            short bitsPerSample = 0;

            while (fs.Position < fs.Length - 8)
            {
                string chunkId = new string(reader.ReadChars(4));
                int chunkSize = reader.ReadInt32();

                if (chunkId == "fmt ")
                {
                    short audioFormat = reader.ReadInt16();
                    channels = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    reader.ReadInt32(); // byte rate
                    reader.ReadInt16(); // block align
                    bitsPerSample = reader.ReadInt16();

                    if (audioFormat != 1) // 1 = PCM
                    {
                        return new StegoCarrierInfo { FilePath = filePath, IsSuitable = false, StatusMessage = "Only uncompressed PCM WAV is supported." };
                    }
                    if (bitsPerSample != 16)
                    {
                        return new StegoCarrierInfo { FilePath = filePath, IsSuitable = false, StatusMessage = "Only 16-bit PCM WAV is supported." };
                    }

                    if (chunkSize > 16)
                    {
                        fs.Seek(chunkSize - 16, SeekOrigin.Current);
                    }
                }
                else if (chunkId == "data")
                {
                    audioDataSize = chunkSize;
                    break;
                }
                else
                {
                    fs.Seek(chunkSize, SeekOrigin.Current);
                }
            }

            if (audioDataSize <= 0)
            {
                return new StegoCarrierInfo { FilePath = filePath, IsSuitable = false, StatusMessage = "No audio data chunk found in WAV." };
            }

            // Each 16-bit sample (2 bytes) carries 1 bit in its LSB
            long sampleCount = audioDataSize / 2;
            long totalCapacityBytes = sampleCount / 8;
            long usableCapacityBytes = Math.Max(0, totalCapacityBytes - PayloadContainer.ContainerOverhead);

            return new StegoCarrierInfo
            {
                FilePath = filePath,
                CarrierType = CarrierType.WavAudio,
                Channels = channels,
                TotalCarrierSizeBytes = fs.Length,
                UsableCapacityBytes = usableCapacityBytes,
                IsSuitable = usableCapacityBytes > 100,
                StatusMessage = "Suitable for WAV PCM steganography."
            };
        }
        catch (Exception ex)
        {
            return new StegoCarrierInfo
            {
                FilePath = filePath,
                CarrierType = CarrierType.WavAudio,
                IsSuitable = false,
                StatusMessage = $"Failed to parse WAV carrier: {ex.Message}"
            };
        }
    }
}
