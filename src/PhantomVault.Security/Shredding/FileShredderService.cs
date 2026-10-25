using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using PhantomVault.Core.Interfaces;

namespace PhantomVault.Security.Shredding;

/// <summary>
/// Implements DoD 5220.22-M compliant multi-pass secure file shredding.
/// Overwrites file data with fixed patterns and cryptographic entropy, flushes physical storage sectors,
/// strips filesystem metadata, and unlinks the inode/MFT entry.
/// </summary>
public class FileShredderService : ISecureShredderService
{
    private const int BufferSize = 65536; // 64 KiB buffer

    public async Task ShredFileAsync(
        string filePath, 
        int passes = 3, 
        IProgress<double>? progress = null, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        if (!File.Exists(filePath))
            return; // Already non-existent

        // Strip readonly and system attributes
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.IsReadOnly)
        {
            fileInfo.IsReadOnly = false;
        }
        File.SetAttributes(filePath, FileAttributes.Normal);

        long fileLength = fileInfo.Length;
        byte[] buffer = new byte[BufferSize];

        if (fileLength > 0)
        {
            // Open with WriteThrough to force physical sector flush
            await using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.None, BufferSize, FileOptions.WriteThrough))
            {
                for (int pass = 1; pass <= passes; pass++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    stream.Position = 0;
                    long bytesRemaining = fileLength;

                    // Pass 1: 0x00, Pass 2: 0xFF, Pass 3+: Cryptographic Random
                    byte fillByte = pass % 2 == 1 ? (byte)0x00 : (byte)0xFF;
                    bool isRandomPass = pass == passes || pass >= 3;

                    if (!isRandomPass)
                    {
                        Array.Fill(buffer, fillByte);
                    }

                    while (bytesRemaining > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int bytesToWrite = (int)Math.Min(bytesRemaining, BufferSize);

                        if (isRandomPass)
                        {
                            RandomNumberGenerator.Fill(buffer.AsSpan(0, bytesToWrite));
                        }

                        await stream.WriteAsync(buffer.AsMemory(0, bytesToWrite), cancellationToken);
                        bytesRemaining -= bytesToWrite;

                        double passProgress = (fileLength - bytesRemaining) / (double)fileLength;
                        double overallProgress = ((pass - 1) + passProgress) / passes;
                        progress?.Report(overallProgress);
                    }

                    // Force disk controller to commit written sectors
                    await stream.FlushAsync(cancellationToken);
                }

                // Truncate file allocation size
                stream.SetLength(0);
                await stream.FlushAsync(cancellationToken);
            }
        }

        // Anti-Forensics: Scramble filesystem timestamps before deletion
        try
        {
            File.SetCreationTimeUtc(filePath, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(filePath, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastAccessTimeUtc(filePath, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        }
        catch
        {
            // Ignore timestamp manipulation errors on restricted file systems
        }

        // Obfuscate directory entry / MFT record by renaming to random GUID
        string directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        string tempPath = Path.Combine(directory, Guid.NewGuid().ToString("N"));

        try
        {
            File.Move(filePath, tempPath);
            File.Delete(tempPath);
        }
        catch
        {
            // Fallback to direct delete if rename fails
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        progress?.Report(1.0);
    }
}
