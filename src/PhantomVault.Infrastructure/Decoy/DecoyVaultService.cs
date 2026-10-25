using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using PhantomVault.Core.Interfaces;
using PhantomVault.Core.Models;

namespace PhantomVault.Infrastructure.Decoy;

/// <summary>
/// Implements mathematical plausible deniability under physical duress or coercion.
/// Intercepts duress passwords to seamlessly unseal an isolated decoy vault with realistic cover files.
/// </summary>
public class DecoyVaultService : IDecoyVaultService
{
    private readonly IVaultRepository _vaultRepo;
    private readonly IVaultFileManager _fileManager;
    private readonly ICryptographyService _crypto;

    public DecoyVaultService(
        IVaultRepository vaultRepo,
        IVaultFileManager fileManager,
        ICryptographyService crypto)
    {
        _vaultRepo = vaultRepo;
        _fileManager = fileManager;
        _crypto = crypto;
    }

    public async Task<bool> HasDuressPasswordAsync(Guid vaultId)
    {
        var vaults = await _vaultRepo.GetVaultsAsync().ConfigureAwait(false);
        var descriptor = FindVault(vaults, vaultId);
        if (descriptor == null) return false;

        string duressConfig = Path.Combine(descriptor.VaultPath, "vault.duress");
        return File.Exists(duressConfig);
    }

    public async Task SetDuressPasswordAsync(Guid vaultId, string duressPassword)
    {
        var vaults = await _vaultRepo.GetVaultsAsync().ConfigureAwait(false);
        var descriptor = FindVault(vaults, vaultId);
        if (descriptor == null)
            throw new InvalidOperationException("Vault not found.");

        string decoyDir = Path.Combine(descriptor.VaultPath, ".decoy");
        Directory.CreateDirectory(decoyDir);

        // Create the isolated Decoy Vault if not existing
        string decoyHeader = Path.Combine(decoyDir, "vault.header");
        VaultDescriptor decoyDesc;
        if (!File.Exists(decoyHeader))
        {
            decoyDesc = await _vaultRepo.CreateVaultAsync(
                $"{descriptor.Name} (Decoy)", 
                decoyDir, 
                duressPassword).ConfigureAwait(false);

            await PopulateRealisticDecoyFilesAsync(decoyDesc.Id).ConfigureAwait(false);
            await _vaultRepo.LockVaultAsync(decoyDesc.Id).ConfigureAwait(false);
        }
        else
        {
            decoyDesc = await _vaultRepo.OpenVaultAsync(decoyDir).ConfigureAwait(false);
        }

        // Save duress verifier
        byte[] salt = _crypto.GenerateSecureRandomBytes(16);
        byte[] verifier = HashDuressPassword(duressPassword, salt);

        string duressConfig = Path.Combine(descriptor.VaultPath, "vault.duress");
        using (var fs = new FileStream(duressConfig, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write(salt);
            bw.Write(verifier.Length);
            bw.Write(verifier);
            bw.Write(decoyDesc.Id.ToByteArray());
        }
    }

    public async Task<bool> VerifyIsDuressPasswordAsync(Guid vaultId, string candidatePassword)
    {
        var vaults = await _vaultRepo.GetVaultsAsync().ConfigureAwait(false);
        var descriptor = FindVault(vaults, vaultId);
        if (descriptor == null) return false;

        string duressConfig = Path.Combine(descriptor.VaultPath, "vault.duress");
        if (!File.Exists(duressConfig)) return false;

        try
        {
            byte[] fileBytes = await File.ReadAllBytesAsync(duressConfig).ConfigureAwait(false);
            if (fileBytes.Length < 16 + 4 + 32) return false;

            byte[] salt = new byte[16];
            Buffer.BlockCopy(fileBytes, 0, salt, 0, 16);

            int verifierLen = BitConverter.ToInt32(fileBytes, 16);
            if (verifierLen != 32 || fileBytes.Length < 20 + 32) return false;

            byte[] storedVerifier = new byte[32];
            Buffer.BlockCopy(fileBytes, 20, storedVerifier, 0, 32);

            byte[] candidateVerifier = HashDuressPassword(candidatePassword, salt);
            return CryptographicOperations.FixedTimeEquals(storedVerifier, candidateVerifier);
        }
        catch
        {
            return false;
        }
    }

    public async Task<VaultDescriptor> EnsureDecoyVaultAsync(Guid masterVaultId, string duressPassword)
    {
        var vaults = await _vaultRepo.GetVaultsAsync().ConfigureAwait(false);
        var masterDesc = FindVault(vaults, masterVaultId);
        if (masterDesc == null)
            throw new InvalidOperationException("Master vault not found.");

        string decoyDir = Path.Combine(masterDesc.VaultPath, ".decoy");
        var decoyVault = await _vaultRepo.OpenVaultAsync(decoyDir).ConfigureAwait(false);
        await _vaultRepo.UnlockVaultAsync(decoyVault.Id, duressPassword).ConfigureAwait(false);
        return decoyVault;
    }

    public async Task PopulateRealisticDecoyFilesAsync(Guid decoyVaultId)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "PV_Decoy_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // 1. Realistic Tax Receipt
            string taxFile = Path.Combine(tempDir, "Tax_Filing_Summary_2024.txt");
            await File.WriteAllTextAsync(taxFile,
                "====================================================\n" +
                "ANNUAL TAX RETURN SUMMARY — ASSESSMENT YEAR 2024-25\n" +
                "====================================================\n" +
                "Taxpayer Status: Individual / Resident\n" +
                "Gross Total Income: $84,250.00\n" +
                "Standard Deduction: $13,850.00\n" +
                "Retirement Contribution (401k): $6,500.00\n" +
                "Health Savings Account (HSA): $3,850.00\n" +
                "Total Tax Deductions Claimed: $24,200.00\n" +
                "Net Taxable Income: $60,050.00\n" +
                "Tax Payable: $8,712.00\n" +
                "TDS / Advance Tax Paid: $9,120.00\n" +
                "Refund Due: $408.00\n" +
                "Filing Status: Accepted & Processed\n" +
                "Verification Hash: e8f921ab4c88310d54a2\n").ConfigureAwait(false);

            // 2. Realistic Travel Itinerary
            string travelFile = Path.Combine(tempDir, "Family_Vacation_Itinerary_Tokyo.txt");
            await File.WriteAllTextAsync(travelFile,
                "====================================================\n" +
                "TOKYO & KYOTO FAMILY TRAVEL RESERVATION\n" +
                "====================================================\n" +
                "Departure: SFO -> HND (Flight NH007) - Oct 12, 11:30 AM\n" +
                "Return: HND -> SFO (Flight NH008) - Oct 22, 05:45 PM\n\n" +
                "Accommodation:\n" +
                "• Tokyo: Shinjuku Prince Hotel (Confirm: #TK-88912)\n" +
                "• Kyoto: Ryokan Gion Sano (Confirm: #KY-44120)\n\n" +
                "Planned Excursions:\n" +
                "1. Senso-ji Temple & Asakusa Market (Oct 13)\n" +
                "2. TeamLab Planets Digital Art (Oct 14)\n" +
                "3. Shinkansen Bullet Train to Kyoto (Oct 16)\n" +
                "4. Fushimi Inari Taisha Shrine (Oct 17)\n" +
                "5. Arashiyama Bamboo Grove (Oct 18)\n").ConfigureAwait(false);

            // 3. Household Ledger
            string ledgerFile = Path.Combine(tempDir, "Household_Expenses_Ledger.csv");
            await File.WriteAllTextAsync(ledgerFile,
                "Date,Category,Description,Amount,PaymentMethod\n" +
                "2024-09-01,Housing,Monthly Apartment Lease,2150.00,Wire Transfer\n" +
                "2024-09-03,Groceries,Whole Foods Market,184.50,Credit Card\n" +
                "2024-09-07,Utilities,City Electric & Water,142.18,Direct Debit\n" +
                "2024-09-11,Transport,Metro Commute Pass,95.00,Debit Card\n" +
                "2024-09-15,Healthcare,Dental Routine Checkup,120.00,FSA Card\n" +
                "2024-09-20,Groceries,Trader Joe's,138.25,Credit Card\n" +
                "2024-09-25,Internet,Fiber Gigabit Service,79.99,Direct Debit\n").ConfigureAwait(false);

            // 4. Project Notes
            string notesFile = Path.Combine(tempDir, "Home_Improvement_Estimates.txt");
            await File.WriteAllTextAsync(notesFile,
                "====================================================\n" +
                "HOME REMODELING CONTRACTOR ESTIMATES\n" +
                "====================================================\n" +
                "1. Kitchen Countertop Granite Replacement: $3,200 - $3,800\n" +
                "2. Hardwood Floor Refinishing (Living Area): $1,850\n" +
                "3. Double-Pane Insulated Window Upgrades: $4,500\n" +
                "4. Exterior Weatherproof Repainting: $2,900\n\n" +
                "Contractor Contact: Summit Builders (Mike H. 555-0194)\n").ConfigureAwait(false);

            // Import all 4 files into the decoy vault
            await _fileManager.ImportFileAsync(decoyVaultId, taxFile).ConfigureAwait(false);
            await _fileManager.ImportFileAsync(decoyVaultId, travelFile).ConfigureAwait(false);
            await _fileManager.ImportFileAsync(decoyVaultId, ledgerFile).ConfigureAwait(false);
            await _fileManager.ImportFileAsync(decoyVaultId, notesFile).ConfigureAwait(false);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static VaultDescriptor? FindVault(IReadOnlyList<VaultDescriptor> list, Guid id)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Id == id) return list[i];
        }
        return null;
    }

    private static byte[] HashDuressPassword(string password, byte[] salt)
    {
        using var hmac = new HMACSHA256(salt);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
    }
}
