using System;
using System.Threading.Tasks;
using Windows.Security.Credentials.UI;
using PhantomVault.Core.Interfaces;

namespace PhantomVault_App.Services;

/// <summary>
/// Hardware-backed biometric (Fingerprint, Facial Recognition) and PIN authentication
/// using Windows Hello via Windows.Security.Credentials.UI.UserConsentVerifier.
/// </summary>
public class WindowsHelloService : IWindowsHelloService
{
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var ucvAvailability = await UserConsentVerifier.CheckAvailabilityAsync();
            return ucvAvailability == UserConsentVerifierAvailability.Available;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> VerifyUserAsync(string promptMessage)
    {
        try
        {
            string message = string.IsNullOrWhiteSpace(promptMessage)
                ? "PHANTOMVAULT Authentication: Confirm your identity to unlock."
                : promptMessage;

            var verificationResult = await UserConsentVerifier.RequestVerificationAsync(message);
            return verificationResult == UserConsentVerificationResult.Verified;
        }
        catch
        {
            return false;
        }
    }
}
