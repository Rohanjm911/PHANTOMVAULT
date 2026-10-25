using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using PhantomVault.Core.Exceptions;
using PhantomVault.Core.Interfaces;
using PhantomVault.Core.Models;

namespace PhantomVault.Steganography;

/// <summary>
/// Unified steganography service orchestrating PNG image and WAV audio concealment.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SteganographyService : ISteganographyService
{
    private readonly ICryptographyService _crypto;
    private readonly IKeyDerivationService _kdf;

    public SteganographyService(ICryptographyService crypto, IKeyDerivationService kdf)
    {
        _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
        _kdf = kdf ?? throw new ArgumentNullException(nameof(kdf));
    }

    public Task<StegoCarrierInfo> AnalyzeCarrierAsync(string carrierFilePath)
    {
        return Task.FromResult(CapacityCalculator.AnalyzeCarrier(carrierFilePath));
    }

    public async Task<string> EmbedPayloadAsync(
        string carrierFilePath, 
        string payloadFilePath, 
        string password, 
        string outputFilePath, 
        IProgress<double>? progress = null, 
        CancellationToken ct = default)
    {
        string ext = Path.GetExtension(carrierFilePath).ToLowerInvariant();

        if (ext == ".png")
        {
            return await ImageStegoEngine.EmbedAsync(
                carrierFilePath, 
                payloadFilePath, 
                password, 
                outputFilePath, 
                _crypto, 
                _kdf, 
                progress, 
                ct).ConfigureAwait(false);
        }
        else if (ext == ".wav")
        {
            return await AudioStegoEngine.EmbedAsync(
                carrierFilePath, 
                payloadFilePath, 
                password, 
                outputFilePath, 
                _crypto, 
                _kdf, 
                progress, 
                ct).ConfigureAwait(false);
        }
        else
        {
            throw new UnsupportedFormatException($"Carrier format '{ext}' is not supported. Please select a .png or .wav file.");
        }
    }

    public async Task<StegoPayloadInfo> ExtractPayloadAsync(
        string carrierFilePath, 
        string password, 
        string outputDirectoryPath, 
        IProgress<double>? progress = null, 
        CancellationToken ct = default)
    {
        string ext = Path.GetExtension(carrierFilePath).ToLowerInvariant();

        if (ext == ".png")
        {
            return await ImageStegoEngine.ExtractAsync(
                carrierFilePath, 
                password, 
                outputDirectoryPath, 
                _crypto, 
                _kdf, 
                progress, 
                ct).ConfigureAwait(false);
        }
        else if (ext == ".wav")
        {
            return await AudioStegoEngine.ExtractAsync(
                carrierFilePath, 
                password, 
                outputDirectoryPath, 
                _crypto, 
                _kdf, 
                progress, 
                ct).ConfigureAwait(false);
        }
        else
        {
            throw new UnsupportedFormatException($"Carrier format '{ext}' is not supported. Please select a .png or .wav file.");
        }
    }
}
