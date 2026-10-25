using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace PhantomVault_App.Helpers;

/// <summary>
/// Ultra-low-latency native Windows audio player using winmm.dll.
/// Works 100% offline with zero dependencies and supports both in-memory byte streams and file paths.
/// </summary>
public static class NativeAudioPlayer
{
    private const uint SND_SYNC = 0x0000;
    private const uint SND_ASYNC = 0x0001;
    private const uint SND_NODEFAULT = 0x0002;
    private const uint SND_MEMORY = 0x0004;
    private const uint SND_PURGE = 0x0040;
    private const uint SND_FILENAME = 0x00020000;

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool PlaySoundFile(string? pszSound, IntPtr hmod, uint fdwSound);

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", SetLastError = true)]
    private static extern bool PlaySoundBytes(byte[]? ptrToWave, IntPtr hmod, uint fdwSound);

    public static void PlayFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;
        Task.Run(() =>
        {
            try
            {
                PlaySoundFile(filePath, IntPtr.Zero, SND_FILENAME | SND_ASYNC | SND_NODEFAULT);
            }
            catch
            {
                // Audio subsystem unavailable
            }
        });
    }

    public static void PlayMemory(byte[] wavBytes)
    {
        if (wavBytes == null || wavBytes.Length == 0) return;
        Task.Run(() =>
        {
            try
            {
                // Play synchronously on background worker thread to guarantee buffer stays pinned
                PlaySoundBytes(wavBytes, IntPtr.Zero, SND_MEMORY | SND_SYNC | SND_NODEFAULT);
            }
            catch
            {
                // Audio subsystem unavailable
            }
        });
    }

    public static void Stop()
    {
        try
        {
            PlaySoundFile((string?)null, IntPtr.Zero, SND_PURGE);
        }
        catch
        {
            // Ignore
        }
    }
}
