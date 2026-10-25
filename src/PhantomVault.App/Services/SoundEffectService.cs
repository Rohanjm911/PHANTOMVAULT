using System;
using System.IO;
using System.Threading.Tasks;
using PhantomVault.Core.Interfaces;
using PhantomVault_App.Helpers;

namespace PhantomVault_App.Services;

/// <summary>
/// High-fidelity tactical cyber audio engine.
/// Generates and caches pure 16-bit PCM 44.1kHz WAV streams in memory with zero disk leakage.
/// </summary>
public class SoundEffectService : ISoundEffectService
{
    private const int SampleRate = 44100;
    private readonly byte[] _unlockWav;
    private readonly byte[] _lockWav;
    private readonly byte[] _emergencyWav;
    private readonly byte[] _shredWav;
    private readonly byte[] _stegoWav;

    public bool IsSoundEnabled { get; set; } = true;

    public SoundEffectService()
    {
        _unlockWav = GenerateUnlockSound();
        _lockWav = GenerateLockSound();
        _emergencyWav = GenerateEmergencySound();
        _shredWav = GenerateShredSound();
        _stegoWav = GenerateStegoSound();
    }

    public void PlayUnlockSound() => PlayWav(_unlockWav);
    public void PlayLockSound() => PlayWav(_lockWav);
    public void PlayEmergencySound() => PlayWav(_emergencyWav);
    public void PlayShredSound() => PlayWav(_shredWav);
    public void PlayStegoSound() => PlayWav(_stegoWav);

    private void PlayWav(byte[] wavBytes)
    {
        if (!IsSoundEnabled || wavBytes == null || wavBytes.Length == 0) return;
        NativeAudioPlayer.PlayMemory(wavBytes);
    }

    #region Mathematical Sound Wave Synthesis

    /// <summary>
    /// Triple harmonic ascending electronic chime (C5 -> E5 -> G5) with exponential decay.
    /// </summary>
    private static byte[] GenerateUnlockSound()
    {
        int durationMs = 280;
        int totalSamples = (SampleRate * durationMs) / 1000;
        short[] samples = new short[totalSamples];

        double[] freqs = { 523.25, 659.25, 783.99 };
        int segmentLength = totalSamples / 3;

        for (int i = 0; i < totalSamples; i++)
        {
            int segment = Math.Min(i / segmentLength, freqs.Length - 1);
            double freq = freqs[segment];
            double t = (double)i / SampleRate;
            double localT = (double)(i % segmentLength) / SampleRate;

            // Envelope: quick attack, smooth exponential decay
            double envelope = Math.Exp(-5.0 * localT * (SampleRate / (double)segmentLength));
            double fundamental = Math.Sin(2.0 * Math.PI * freq * t);
            double harmonic = 0.25 * Math.Sin(4.0 * Math.PI * freq * t);

            samples[i] = (short)((fundamental + harmonic) * 14000 * envelope);
        }

        return EncodePcmToWav(samples);
    }

    /// <summary>
    /// Low electromagnetic latch and dampening lock pulse (440 Hz down to 180 Hz).
    /// </summary>
    private static byte[] GenerateLockSound()
    {
        int durationMs = 220;
        int totalSamples = (SampleRate * durationMs) / 1000;
        short[] samples = new short[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            double progress = (double)i / totalSamples;
            double freq = 440.0 - (260.0 * progress);
            double t = (double)i / SampleRate;
            double envelope = Math.Exp(-8.0 * progress);

            double sample = Math.Sin(2.0 * Math.PI * freq * t);
            samples[i] = (short)(sample * 16000 * envelope);
        }

        return EncodePcmToWav(samples);
    }

    /// <summary>
    /// Tactical dual-tone military lockdown alert warble (880 Hz & 1175 Hz).
    /// </summary>
    private static byte[] GenerateEmergencySound()
    {
        int durationMs = 360;
        int totalSamples = (SampleRate * durationMs) / 1000;
        short[] samples = new short[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            double t = (double)i / SampleRate;
            // Rapid alternation between 880Hz and 1175Hz
            double freq = ((i / (SampleRate / 18)) % 2 == 0) ? 880.0 : 1174.66;
            double progress = (double)i / totalSamples;
            double envelope = 1.0 - (progress * 0.3); // Consistent urgent volume

            double sample = Math.Sin(2.0 * Math.PI * freq * t);
            samples[i] = (short)(sample * 18000 * envelope);
        }

        return EncodePcmToWav(samples);
    }

    /// <summary>
    /// Digital forensic wipe swoosh (downward filter sweep + noise burst).
    /// </summary>
    private static byte[] GenerateShredSound()
    {
        int durationMs = 300;
        int totalSamples = (SampleRate * durationMs) / 1000;
        short[] samples = new short[totalSamples];
        var rng = new Random(42);

        for (int i = 0; i < totalSamples; i++)
        {
            double progress = (double)i / totalSamples;
            double freq = 1600.0 * Math.Exp(-6.0 * progress);
            double t = (double)i / SampleRate;
            double envelope = Math.Exp(-4.5 * progress);

            double tone = Math.Sin(2.0 * Math.PI * freq * t);
            double noise = (rng.NextDouble() * 2.0 - 1.0) * 0.4;

            samples[i] = (short)((tone * 0.6 + noise) * 15000 * envelope);
        }

        return EncodePcmToWav(samples);
    }

    /// <summary>
    /// High-frequency covert frequency-modulated acoustic chirp (1400 Hz -> 1800 Hz).
    /// </summary>
    private static byte[] GenerateStegoSound()
    {
        int durationMs = 120;
        int totalSamples = (SampleRate * durationMs) / 1000;
        short[] samples = new short[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            double progress = (double)i / totalSamples;
            double freq = 1400.0 + (400.0 * progress);
            double t = (double)i / SampleRate;
            double envelope = Math.Sin(Math.PI * progress); // Soft bell envelope

            double sample = Math.Sin(2.0 * Math.PI * freq * t);
            samples[i] = (short)(sample * 12000 * envelope);
        }

        return EncodePcmToWav(samples);
    }

    /// <summary>
    /// Encodes a 16-bit mono signed PCM buffer into standard canonical RIFF/WAVE bytes.
    /// </summary>
    private static byte[] EncodePcmToWav(short[] pcmSamples)
    {
        int subchunk2Size = pcmSamples.Length * sizeof(short);
        int chunkSize = 36 + subchunk2Size;

        using var ms = new MemoryStream(44 + subchunk2Size);
        using var bw = new BinaryWriter(ms);

        // RIFF header
        bw.Write(new char[] { 'R', 'I', 'F', 'F' });
        bw.Write(chunkSize);
        bw.Write(new char[] { 'W', 'A', 'V', 'E' });

        // fmt chunk
        bw.Write(new char[] { 'f', 'm', 't', ' ' });
        bw.Write(16); // Subchunk1Size for PCM
        bw.Write((short)1); // AudioFormat: 1 = PCM
        bw.Write((short)1); // NumChannels: 1 = Mono
        bw.Write(SampleRate);
        bw.Write(SampleRate * 1 * sizeof(short)); // ByteRate
        bw.Write((short)(1 * sizeof(short))); // BlockAlign
        bw.Write((short)16); // BitsPerSample

        // data chunk
        bw.Write(new char[] { 'd', 'a', 't', 'a' });
        bw.Write(subchunk2Size);

        for (int i = 0; i < pcmSamples.Length; i++)
        {
            bw.Write(pcmSamples[i]);
        }

        bw.Flush();
        return ms.ToArray();
    }

    #endregion
}
