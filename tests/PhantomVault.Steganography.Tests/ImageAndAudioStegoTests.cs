using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using PhantomVault.Core.Exceptions;
using PhantomVault.Security.Cryptography;
using PhantomVault.Security.KeyManagement;
using PhantomVault.Steganography;
using Xunit;

namespace PhantomVault.Steganography.Tests;

[SupportedOSPlatform("windows")]
public class ImageAndAudioStegoTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly AesGcmCryptoProvider _crypto;
    private readonly KeyDerivationService _kdf;
    private readonly SteganographyService _stegoService;

    public ImageAndAudioStegoTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), "PhantomVaultStegoTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testBaseDir);

        _crypto = new AesGcmCryptoProvider();
        _kdf = new KeyDerivationService();
        _stegoService = new SteganographyService(_crypto, _kdf);
    }

    [Fact]
    public async Task PNG_EmbedAndExtract_RoundTripSucceeds()
    {
        string carrierPath = Path.Combine(_testBaseDir, "carrier.png");
        string payloadPath = Path.Combine(_testBaseDir, "secret.txt");
        string outputPath = Path.Combine(_testBaseDir, "carrier_embedded.png");
        string extractDir = Path.Combine(_testBaseDir, "extracted");
        string password = "StegoSecretPassphrase123!";

        // 1. Create a 150x150 carrier PNG (usable capacity ~ 8.4 KiB)
        using (var bmp = new Bitmap(150, 150, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.CornflowerBlue);
                g.FillEllipse(Brushes.WhiteSmoke, 20, 20, 100, 100);
            }
            bmp.Save(carrierPath, ImageFormat.Png);
        }

        // 2. Create payload file
        string secretText = "TOP SECRET: Agent rendezvous coordinates: 48.8584 N, 2.2945 E.";
        await File.WriteAllTextAsync(payloadPath, secretText, Encoding.UTF8);

        // 3. Analyze capacity
        var info = await _stegoService.AnalyzeCarrierAsync(carrierPath);
        Assert.True(info.IsSuitable);
        Assert.True(info.UsableCapacityBytes > 1000);

        // 4. Embed
        await _stegoService.EmbedPayloadAsync(carrierPath, payloadPath, password, outputPath);
        Assert.True(File.Exists(outputPath));

        // 5. Extract with correct password
        var extractedInfo = await _stegoService.ExtractPayloadAsync(outputPath, password, extractDir);
        Assert.NotNull(extractedInfo);
        Assert.Equal("secret.txt", extractedInfo.OriginalFileName);

        string extractedPath = Path.Combine(extractDir, "secret.txt");
        Assert.True(File.Exists(extractedPath));
        string extractedText = await File.ReadAllTextAsync(extractedPath, Encoding.UTF8);
        Assert.Equal(secretText, extractedText);
    }

    [Fact]
    public async Task PNG_Extract_WrongPassword_ThrowsInvalidCredentialsException()
    {
        string carrierPath = Path.Combine(_testBaseDir, "carrier_auth.png");
        string payloadPath = Path.Combine(_testBaseDir, "auth_secret.txt");
        string outputPath = Path.Combine(_testBaseDir, "carrier_auth_out.png");
        string extractDir = Path.Combine(_testBaseDir, "extracted_fail");

        using (var bmp = new Bitmap(100, 100, PixelFormat.Format32bppArgb))
        {
            bmp.Save(carrierPath, ImageFormat.Png);
        }
        await File.WriteAllTextAsync(payloadPath, "Testing wrong password behavior");

        await _stegoService.EmbedPayloadAsync(carrierPath, payloadPath, "CorrectPassword123!", outputPath);

        await Assert.ThrowsAsync<InvalidCredentialsException>(async () =>
            await _stegoService.ExtractPayloadAsync(outputPath, "IncorrectPassword!", extractDir));
    }

    [Fact]
    public async Task PNG_Extract_NonStegoCarrier_ThrowsVaultCorruptedException()
    {
        string pristineCarrier = Path.Combine(_testBaseDir, "pristine.png");
        using (var bmp = new Bitmap(80, 80, PixelFormat.Format32bppArgb))
        {
            bmp.Save(pristineCarrier, ImageFormat.Png);
        }

        string extractDir = Path.Combine(_testBaseDir, "extracted_fail");

        await Assert.ThrowsAsync<VaultCorruptedException>(async () =>
            await _stegoService.ExtractPayloadAsync(pristineCarrier, "AnyPassword", extractDir));
    }

    [Fact]
    public async Task PNG_OversizedPayload_ThrowsCarrierCapacityExceededException()
    {
        string carrierPath = Path.Combine(_testBaseDir, "small_carrier.png");
        string payloadPath = Path.Combine(_testBaseDir, "giant_payload.bin");
        string outputPath = Path.Combine(_testBaseDir, "oversized_out.png");

        // 50x50 image = 2500 pixels * 3 = 7500 bits = ~937 bytes total capacity (usable ~870 bytes)
        using (var bmp = new Bitmap(50, 50, PixelFormat.Format32bppArgb))
        {
            bmp.Save(carrierPath, ImageFormat.Png);
        }

        // 10 KiB payload definitely exceeds 870 bytes
        byte[] giant = new byte[10 * 1024];
        await File.WriteAllBytesAsync(payloadPath, giant);

        await Assert.ThrowsAsync<CarrierCapacityExceededException>(async () =>
            await _stegoService.EmbedPayloadAsync(carrierPath, payloadPath, "Password", outputPath));
    }

    [Fact]
    public async Task WAV_EmbedAndExtract_RoundTripSucceeds()
    {
        string carrierWav = Path.Combine(_testBaseDir, "audio_carrier.wav");
        string payloadPath = Path.Combine(_testBaseDir, "audio_secret.txt");
        string outputWav = Path.Combine(_testBaseDir, "audio_embedded.wav");
        string extractDir = Path.Combine(_testBaseDir, "audio_extracted");
        string password = "WavStegoSecretPassword123!";

        // Synthesize 1 second 44.1kHz 16-bit mono PCM WAV (44,100 samples = ~5,500 bytes capacity)
        CreateSyntheticPcmWav(carrierWav, 44100);

        string secretMsg = "Audio concealment transmission: Alpha-Omega-77.";
        await File.WriteAllTextAsync(payloadPath, secretMsg, Encoding.UTF8);

        // Analyze
        var info = await _stegoService.AnalyzeCarrierAsync(carrierWav);
        Assert.True(info.IsSuitable);
        Assert.True(info.UsableCapacityBytes > 2000);

        // Embed
        await _stegoService.EmbedPayloadAsync(carrierWav, payloadPath, password, outputWav);
        Assert.True(File.Exists(outputWav));

        // Extract
        var extractedInfo = await _stegoService.ExtractPayloadAsync(outputWav, password, extractDir);
        Assert.Equal("audio_secret.txt", extractedInfo.OriginalFileName);

        string recovered = await File.ReadAllTextAsync(Path.Combine(extractDir, "audio_secret.txt"), Encoding.UTF8);
        Assert.Equal(secretMsg, recovered);
    }

    private static void CreateSyntheticPcmWav(string path, int numSamples)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var w = new BinaryWriter(fs);

        int sampleRate = 44100;
        short channels = 1;
        short bitsPerSample = 16;
        int dataSize = numSamples * (bitsPerSample / 8) * channels;

        // RIFF header
        w.Write("RIFF".ToCharArray());
        w.Write(36 + dataSize);
        w.Write("WAVE".ToCharArray());

        // fmt chunk
        w.Write("fmt ".ToCharArray());
        w.Write(16); // chunk size
        w.Write((short)1); // PCM
        w.Write(channels);
        w.Write(sampleRate);
        w.Write(sampleRate * channels * (bitsPerSample / 8)); // byte rate
        w.Write((short)(channels * (bitsPerSample / 8)));     // block align
        w.Write(bitsPerSample);

        // data chunk
        w.Write("data".ToCharArray());
        w.Write(dataSize);

        // Generate synthetic sine tone
        for (int i = 0; i < numSamples; i++)
        {
            short sample = (short)(Math.Sin(2 * Math.PI * 440 * i / sampleRate) * 16000);
            w.Write(sample);
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testBaseDir))
            {
                Directory.Delete(_testBaseDir, recursive: true);
            }
        }
        catch { }
    }
}
