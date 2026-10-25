using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using PhantomVault.Core.Enums;
using PhantomVault.Core.Exceptions;
using PhantomVault.Security.Cryptography;
using PhantomVault.Security.KeyManagement;
using PhantomVault.Storage;
using Xunit;

namespace PhantomVault.Storage.Tests;

public class VaultRepositoryAndFileManagerTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly AesGcmCryptoProvider _crypto;
    private readonly KeyDerivationService _kdf;
    private readonly KeyWrappingService _keyWrapper;
    private readonly VaultRepository _vaultRepo;
    private readonly VaultFileManager _fileManager;

    public VaultRepositoryAndFileManagerTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), "PhantomVaultTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testBaseDir);

        _crypto = new AesGcmCryptoProvider();
        _kdf = new KeyDerivationService();
        _keyWrapper = new KeyWrappingService(_crypto);
        _vaultRepo = new VaultRepository(_crypto, _kdf, _keyWrapper);
        _fileManager = new VaultFileManager(_vaultRepo, _crypto);
    }

    [Fact]
    public async Task CreateVault_UnlocksAutomatically_AndCreatesExpectedFiles()
    {
        string vaultName = "TestVaultAlpha";
        string password = "TestVaultPassword123!";

        var vault = await _vaultRepo.CreateVaultAsync(vaultName, _testBaseDir, password, KdfAlgorithm.Argon2id);

        Assert.NotNull(vault);
        Assert.Equal(VaultState.Unlocked, vault.State);
        Assert.True(Directory.Exists(vault.VaultPath));
        Assert.True(File.Exists(Path.Combine(vault.VaultPath, "vault.header")));
        Assert.True(File.Exists(Path.Combine(vault.VaultPath, "catalog.db")));
        Assert.True(Directory.Exists(Path.Combine(vault.VaultPath, "data")));
        Assert.True(_vaultRepo.IsVaultUnlocked(vault.Id));
    }

    [Fact]
    public async Task LockAndUnlock_CorrectPasswordSucceeds_WrongPasswordFails()
    {
        string vaultName = "AuthTestVault";
        string password = "CorrectMasterPassword99!";

        var vault = await _vaultRepo.CreateVaultAsync(vaultName, _testBaseDir, password, KdfAlgorithm.Argon2id);
        Assert.True(_vaultRepo.IsVaultUnlocked(vault.Id));

        // Lock
        await _vaultRepo.LockVaultAsync(vault.Id);
        Assert.False(_vaultRepo.IsVaultUnlocked(vault.Id));

        // Attempt unlock with wrong password
        await Assert.ThrowsAsync<InvalidCredentialsException>(async () =>
            await _vaultRepo.UnlockVaultAsync(vault.Id, "WrongPassword!"));

        // Unlock with correct password
        await _vaultRepo.UnlockVaultAsync(vault.Id, password);
        Assert.True(_vaultRepo.IsVaultUnlocked(vault.Id));
    }

    [Fact]
    public async Task ChangePassword_AllowsUnlockWithNewPasswordOnly()
    {
        string vaultName = "ChangePasswordVault";
        string oldPass = "InitialPass123!";
        string newPass = "NewSecurePass456!";

        var vault = await _vaultRepo.CreateVaultAsync(vaultName, _testBaseDir, oldPass, KdfAlgorithm.Argon2id);

        // Change Password
        await _vaultRepo.ChangePasswordAsync(vault.Id, oldPass, newPass);

        // Lock
        await _vaultRepo.LockVaultAsync(vault.Id);

        // Old password must fail
        await Assert.ThrowsAsync<InvalidCredentialsException>(async () =>
            await _vaultRepo.UnlockVaultAsync(vault.Id, oldPass));

        // New password must succeed
        await _vaultRepo.UnlockVaultAsync(vault.Id, newPass);
        Assert.True(_vaultRepo.IsVaultUnlocked(vault.Id));
    }

    [Fact]
    public async Task ImportAndExportFile_RoundTripMatchesExactContent()
    {
        string vaultName = "FileIoVault";
        string password = "FileIoPassword123!";
        var vault = await _vaultRepo.CreateVaultAsync(vaultName, _testBaseDir, password, KdfAlgorithm.Argon2id);

        // Create sample source file
        string sampleSrc = Path.Combine(_testBaseDir, "secret_document.txt");
        string sampleText = "CONFIDENTIAL: Quantum key distribution parameters and encryption keys.";
        await File.WriteAllTextAsync(sampleSrc, sampleText, Encoding.UTF8);

        // Import
        var entry = await _fileManager.ImportFileAsync(vault.Id, sampleSrc);
        Assert.NotNull(entry);
        Assert.Equal("secret_document.txt", entry.Name);
        Assert.True(entry.PlaintextSizeBytes > 0);
        Assert.True(entry.CiphertextSizeBytes > 0);

        // Export
        string exportedPath = Path.Combine(_testBaseDir, "exported_document.txt");
        await _fileManager.ExportFileAsync(vault.Id, entry.Id, exportedPath);

        Assert.True(File.Exists(exportedPath));
        string exportedText = await File.ReadAllTextAsync(exportedPath, Encoding.UTF8);
        Assert.Equal(sampleText, exportedText);
    }

    [Fact]
    public async Task ExportFile_TamperedPayloadOnDisk_ThrowsIntegrityException()
    {
        string vaultName = "TamperVault";
        string password = "TamperPassword123!";
        var vault = await _vaultRepo.CreateVaultAsync(vaultName, _testBaseDir, password, KdfAlgorithm.Argon2id);

        string sampleSrc = Path.Combine(_testBaseDir, "classified.txt");
        await File.WriteAllTextAsync(sampleSrc, "Classified payload bytes");

        var entry = await _fileManager.ImportFileAsync(vault.Id, sampleSrc);

        // Tamper with payload on disk
        string payloadPath = Path.Combine(vault.VaultPath, "data", $"{entry.Id}.pvc");
        byte[] payloadBytes = await File.ReadAllBytesAsync(payloadPath);
        payloadBytes[payloadBytes.Length - 1] ^= 0xFF; // Flip bit in last chunk tag
        await File.WriteAllBytesAsync(payloadPath, payloadBytes);

        string exportDest = Path.Combine(_testBaseDir, "should_fail.txt");
        await Assert.ThrowsAnyAsync<PhantomVaultException>(async () =>
            await _fileManager.ExportFileAsync(vault.Id, entry.Id, exportDest));

        Assert.False(File.Exists(exportDest)); // Atomic write ensures no corrupted file is left
    }

    [Fact]
    public async Task DeleteEntry_RemovesFromCatalogAndDisk()
    {
        string vaultName = "DeleteVault";
        string password = "DeletePassword123!";
        var vault = await _vaultRepo.CreateVaultAsync(vaultName, _testBaseDir, password, KdfAlgorithm.Argon2id);

        string sampleSrc = Path.Combine(_testBaseDir, "to_delete.txt");
        await File.WriteAllTextAsync(sampleSrc, "Ephemeral data");

        var entry = await _fileManager.ImportFileAsync(vault.Id, sampleSrc);
        string payloadPath = Path.Combine(vault.VaultPath, "data", $"{entry.Id}.pvc");
        Assert.True(File.Exists(payloadPath));

        // Delete
        await _fileManager.DeleteEntryAsync(vault.Id, entry.Id);

        Assert.False(File.Exists(payloadPath));
        var entries = await _fileManager.GetEntriesAsync(vault.Id);
        Assert.DoesNotContain(entries, e => e.Id == entry.Id);
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
