using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhantomVault.Core.Enums;
using PhantomVault.Core.Exceptions;
using PhantomVault.Infrastructure.Backup;
using PhantomVault.Infrastructure.Logging;
using PhantomVault.Security.Cryptography;
using PhantomVault.Security.KeyManagement;
using PhantomVault.Storage;
using Xunit;

namespace PhantomVault.Integration.Tests;

public class EndToEndVaultAndBackupTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly AesGcmCryptoProvider _crypto;
    private readonly KeyDerivationService _kdf;
    private readonly KeyWrappingService _keyWrapper;
    private readonly VaultRepository _vaultRepo;
    private readonly VaultFileManager _fileManager;
    private readonly BackupService _backupService;
    private readonly ActivityLogService _logService;

    public EndToEndVaultAndBackupTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), "PV_Integration_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testBaseDir);

        _crypto = new AesGcmCryptoProvider();
        _kdf = new KeyDerivationService();
        _keyWrapper = new KeyWrappingService(_crypto);
        _vaultRepo = new VaultRepository(_crypto, _kdf, _keyWrapper, Path.Combine(_testBaseDir, "test_registry.json"));
        _fileManager = new VaultFileManager(_vaultRepo, _crypto);
        _backupService = new BackupService(_vaultRepo);
        _logService = new ActivityLogService(Path.Combine(_testBaseDir, "test_activity.db"));
    }

    [Fact]
    public async Task EndToEnd_Create_Import_Backup_Restore_Decrypt_Succeeds()
    {
        string vaultName = "PrimaryVault";
        string masterPass = "UltraMasterPassword#2026!";

        // 1. Create Vault
        var vault = await _vaultRepo.CreateVaultAsync(vaultName, _testBaseDir, masterPass, KdfAlgorithm.Argon2id);
        await _logService.LogEventAsync(ActivityEventType.VaultCreated, ActivitySeverity.Info, vaultName, "Vault created successfully");

        // 2. Import Files
        string file1Src = Path.Combine(_testBaseDir, "report.pdf");
        byte[] pdfDummy = Encoding.UTF8.GetBytes("%PDF-1.7 Confidential Strategic Intelligence Report");
        await File.WriteAllBytesAsync(file1Src, pdfDummy);
        var entry1 = await _fileManager.ImportFileAsync(vault.Id, file1Src);
        await _logService.LogEventAsync(ActivityEventType.FileImported, ActivitySeverity.Info, vaultName, $"Imported {entry1.Name}");

        string file2Src = Path.Combine(_testBaseDir, "notes.txt");
        string notesText = "Emergency meeting minutes: 12.00 UTC.";
        await File.WriteAllTextAsync(file2Src, notesText);
        var entry2 = await _fileManager.ImportFileAsync(vault.Id, file2Src);

        // 3. Create Encrypted Backup
        string backupArchive = Path.Combine(_testBaseDir, "PrimaryVault_Backup.pvbackup");
        await _backupService.CreateBackupAsync(vault.Id, backupArchive);
        Assert.True(File.Exists(backupArchive));
        await _logService.LogEventAsync(ActivityEventType.BackupCreated, ActivitySeverity.Info, vaultName, "Encrypted backup created");

        // 4. Validate Backup
        var manifest = await _backupService.ValidateBackupAsync(backupArchive);
        Assert.NotNull(manifest);
        Assert.Equal(vault.Id, manifest.VaultId);
        Assert.Equal(2, manifest.FileCount);

        // 5. Restore into new Vault
        string restoreDir = Path.Combine(_testBaseDir, "RestoredLocation");
        Directory.CreateDirectory(restoreDir);

        var restoredVault = await _backupService.RestoreBackupAsync(backupArchive, restoreDir, "RestoredVault");
        Assert.NotNull(restoredVault);
        Assert.Equal(VaultState.Locked, restoredVault.State);

        // 6. Unlock restored vault with original password
        await _vaultRepo.UnlockVaultAsync(restoredVault.Id, masterPass);
        Assert.True(_vaultRepo.IsVaultUnlocked(restoredVault.Id));

        // 7. Verify entries inside restored vault
        var entries = await _fileManager.GetEntriesAsync(restoredVault.Id);
        Assert.Equal(2, entries.Count);

        // 8. Export and verify exact content from restored vault
        string exportedNotes = Path.Combine(_testBaseDir, "exported_notes.txt");
        var notesEntry = entries.FirstOrDefault(e => e.Name == "notes.txt")!;
        await _fileManager.ExportFileAsync(restoredVault.Id, notesEntry.Id, exportedNotes);

        string exportedText = await File.ReadAllTextAsync(exportedNotes, Encoding.UTF8);
        Assert.Equal(notesText, exportedText);

        // 9. Verify Activity Logs are sanitized
        var logs = await _logService.GetLogsAsync();
        Assert.True(logs.Count >= 3);
        foreach (var log in logs)
        {
            Assert.DoesNotContain(masterPass, log.Message);
            Assert.DoesNotContain(masterPass, log.Details);
        }
    }

    [Fact]
    public async Task ValidateBackup_TamperedDataInArchive_ThrowsCryptographicIntegrityException()
    {
        string vaultName = "TamperBackupVault";
        string masterPass = "Pass987!";
        var vault = await _vaultRepo.CreateVaultAsync(vaultName, _testBaseDir, masterPass, KdfAlgorithm.Argon2id);

        string fileSrc = Path.Combine(_testBaseDir, "data.bin");
        await File.WriteAllBytesAsync(fileSrc, new byte[1024]);
        await _fileManager.ImportFileAsync(vault.Id, fileSrc);

        string backupArchive = Path.Combine(_testBaseDir, "TamperTest.pvbackup");
        await _backupService.CreateBackupAsync(vault.Id, backupArchive);

        // Corrupt catalog.db inside the zip
        string corruptedArchive = Path.Combine(_testBaseDir, "CorruptedTest.pvbackup");
        File.Copy(backupArchive, corruptedArchive);

        using (var zip = ZipFile.Open(corruptedArchive, ZipArchiveMode.Update))
        {
            var catalog = zip.GetEntry("catalog.db")!;
            catalog.Delete();
            var newCatalog = zip.CreateEntry("catalog.db");
            using var writer = new StreamWriter(newCatalog.Open());
            writer.Write("Corrupted fake catalog content");
        }

        await Assert.ThrowsAsync<CryptographicIntegrityException>(async () =>
            await _backupService.ValidateBackupAsync(corruptedArchive));
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
