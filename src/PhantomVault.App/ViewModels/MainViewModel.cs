using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using PhantomVault.Core.Enums;
using PhantomVault.Core.Exceptions;
using PhantomVault.Core.Interfaces;
using PhantomVault.Core.Models;
using PhantomVault.Storage;

namespace PhantomVault_App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IVaultRepository _vaultRepo;
    private readonly IVaultFileManager _fileManager;
    private readonly ISteganographyService _stegoService;
    private readonly IBackupService _backupService;
    private readonly IActivityLogService _logService;
    private readonly ISecurityPolicyService _policyService;
    private readonly IPasswordPolicyValidator _passwordValidator;

    public MainViewModel(
        IVaultRepository vaultRepo,
        IVaultFileManager fileManager,
        ISteganographyService stegoService,
        IBackupService backupService,
        IActivityLogService logService,
        ISecurityPolicyService policyService,
        IPasswordPolicyValidator passwordValidator)
    {
        _vaultRepo = vaultRepo;
        _fileManager = fileManager;
        _stegoService = stegoService;
        _backupService = backupService;
        _logService = logService;
        _policyService = policyService;
        _passwordValidator = passwordValidator;

        _policyService.DeadMansSwitchCountdownTick += seconds =>
        {
            DeadMansCountdownSeconds = seconds;
            IsDeadMansWarningVisible = seconds > 0;
        };

        _policyService.EmergencyLockTriggered += () =>
        {
            _ = LockAllVaultsAsync();
            ShowStatus("Emergency lock policy triggered. All vaults locked.", InfoBarSeverity.Warning);
        };
    }

    [ObservableProperty]
    private ObservableCollection<VaultDescriptor> _vaults = new();

    [ObservableProperty]
    private VaultDescriptor? _selectedVault;

    [ObservableProperty]
    private ObservableCollection<VaultEntry> _entries = new();

    [ObservableProperty]
    private VaultEntry? _selectedEntry;

    [ObservableProperty]
    private ObservableCollection<ActivityLogRecord> _activityLogs = new();

    [ObservableProperty]
    private bool _isUnlocked;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _busyStatusText = string.Empty;

    [ObservableProperty]
    private double _operationProgress;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private InfoBarSeverity _statusSeverity = InfoBarSeverity.Informational;

    [ObservableProperty]
    private bool _isStatusOpen;

    [ObservableProperty]
    private int _deadMansCountdownSeconds;

    [ObservableProperty]
    private bool _isDeadMansWarningVisible;

    // Stego UI State
    [ObservableProperty]
    private StegoCarrierInfo? _stegoCarrierInfo;

    [ObservableProperty]
    private string _stegoCarrierPath = string.Empty;

    [ObservableProperty]
    private string _stegoPayloadPath = string.Empty;

    [ObservableProperty]
    private string _stegoOutputPath = string.Empty;

    [ObservableProperty]
    private string _stegoPassword = string.Empty;

    [ObservableProperty]
    private string _stegoExtractionCarrierPath = string.Empty;

    [ObservableProperty]
    private string _stegoExtractionPassword = string.Empty;

    [ObservableProperty]
    private string _stegoExtractionOutDir = string.Empty;

    // Backup UI State
    [ObservableProperty]
    private BackupManifest? _inspectedManifest;

    [ObservableProperty]
    private string _inspectedBackupPath = string.Empty;

    // Security Center State
    [ObservableProperty]
    private string _securityPostureLabel = "PROTECTED";

    [ObservableProperty]
    private string _kdfConfigurationDescription = "Argon2id (64 MiB RAM, 4 Iterations, 4 Parallel Lanes)";

    [ObservableProperty]
    private string _lastIntegrityCheckResult = "All files verified intact.";

    public async Task InitializeAsync()
    {
        await LoadVaultsAsync();
        await RefreshActivityLogsAsync();
    }

    public async Task LoadVaultsAsync()
    {
        try
        {
            var list = await _vaultRepo.GetVaultsAsync();
            Vaults.Clear();
            foreach (var v in list)
            {
                Vaults.Add(v);
            }

            if (SelectedVault == null && Vaults.Count > 0)
            {
                SelectedVault = Vaults[0];
            }

            UpdateLockState();
        }
        catch (Exception ex)
        {
            ShowStatus($"Error loading vaults: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    partial void OnSelectedVaultChanged(VaultDescriptor? value)
    {
        UpdateLockState();
        if (value != null && value.State == VaultState.Unlocked)
        {
            _ = LoadEntriesAsync();
        }
        else
        {
            Entries.Clear();
        }
    }

    private void UpdateLockState()
    {
        IsUnlocked = SelectedVault != null && _vaultRepo.IsVaultUnlocked(SelectedVault.Id);
    }

    [RelayCommand]
    public async Task UnlockCurrentVaultAsync(string password)
    {
        if (SelectedVault == null) return;

        try
        {
            IsBusy = true;
            BusyStatusText = "Deriving key and unlocking vault...";
            _policyService.RegisterUserActivity();

            await _vaultRepo.UnlockVaultAsync(SelectedVault.Id, password);
            SelectedVault.State = VaultState.Unlocked;
            UpdateLockState();

            await LoadEntriesAsync();
            await _logService.LogEventAsync(ActivityEventType.VaultUnlocked, ActivitySeverity.Info, SelectedVault.Name, "Vault unlocked successfully.");
            await RefreshActivityLogsAsync();

            ShowStatus($"Vault '{SelectedVault.Name}' unlocked.", InfoBarSeverity.Success);
        }
        catch (InvalidCredentialsException)
        {
            ShowStatus("Incorrect master password. Access denied.", InfoBarSeverity.Error);
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to unlock vault: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task LockCurrentVaultAsync()
    {
        if (SelectedVault == null) return;

        try
        {
            await _vaultRepo.LockVaultAsync(SelectedVault.Id);
            SelectedVault.State = VaultState.Locked;
            UpdateLockState();
            Entries.Clear();

            await _logService.LogEventAsync(ActivityEventType.VaultLocked, ActivitySeverity.Info, SelectedVault.Name, "Vault locked.");
            await RefreshActivityLogsAsync();
            ShowStatus($"Vault '{SelectedVault.Name}' locked and keys cleared from memory.", InfoBarSeverity.Informational);
        }
        catch (Exception ex)
        {
            ShowStatus($"Error locking vault: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    [RelayCommand]
    public async Task LockAllVaultsAsync()
    {
        try
        {
            await _vaultRepo.LockAllVaultsAsync();
            foreach (var v in Vaults)
            {
                v.State = VaultState.Locked;
            }
            UpdateLockState();
            Entries.Clear();

            await _logService.LogEventAsync(ActivityEventType.EmergencyLockTriggered, ActivitySeverity.SecurityAlert, "GLOBAL", "Global emergency lock triggered. All keys destroyed in RAM.");
            await RefreshActivityLogsAsync();
            ShowStatus("All vaults have been locked. Cryptographic keys cleared from volatile RAM.", InfoBarSeverity.Warning);
        }
        catch (Exception ex)
        {
            ShowStatus($"Emergency lock error: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    [RelayCommand]
    public async Task CreateNewVaultAsync((string name, string folderPath, string password, KdfAlgorithm kdf) args)
    {
        try
        {
            IsBusy = true;
            BusyStatusText = "Creating encrypted vault and deriving key...";
            _policyService.RegisterUserActivity();

            var newVault = await _vaultRepo.CreateVaultAsync(args.name, args.folderPath, args.password, args.kdf);
            Vaults.Add(newVault);
            SelectedVault = newVault;

            await _logService.LogEventAsync(ActivityEventType.VaultCreated, ActivitySeverity.Info, newVault.Name, "Vault initialized.");
            await RefreshActivityLogsAsync();

            ShowStatus($"Vault '{newVault.Name}' created and ready.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"Error creating vault: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task LoadEntriesAsync()
    {
        if (SelectedVault == null || !IsUnlocked) return;

        try
        {
            var list = await _fileManager.GetEntriesAsync(SelectedVault.Id);
            Entries.Clear();
            foreach (var e in list)
            {
                if (string.IsNullOrWhiteSpace(SearchQuery) || e.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                {
                    Entries.Add(e);
                }
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Error reading vault entries: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    [RelayCommand]
    public async Task ImportFileAsync(string filePath)
    {
        if (SelectedVault == null || !IsUnlocked) return;

        try
        {
            IsBusy = true;
            BusyStatusText = $"Encrypting and storing {Path.GetFileName(filePath)}...";
            OperationProgress = 0;
            _policyService.RegisterUserActivity();

            var progress = new Progress<double>(p => OperationProgress = p);
            var entry = await _fileManager.ImportFileAsync(SelectedVault.Id, filePath, null, progress);

            Entries.Insert(0, entry);
            SelectedVault.FileCount++;
            SelectedVault.TotalCiphertextBytes += entry.CiphertextSizeBytes;
            SelectedVault.TotalPlaintextBytes += entry.PlaintextSizeBytes;

            await _logService.LogEventAsync(ActivityEventType.FileImported, ActivitySeverity.Info, SelectedVault.Name, $"Encrypted file imported: {entry.Name}");
            await RefreshActivityLogsAsync();

            ShowStatus($"File '{entry.Name}' encrypted and stored in vault.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to import file: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ExportFileAsync((Guid entryId, string destinationPath) args)
    {
        if (SelectedVault == null || !IsUnlocked) return;

        try
        {
            IsBusy = true;
            BusyStatusText = "Decrypting file and verifying SHA-256 integrity...";
            OperationProgress = 0;
            _policyService.RegisterUserActivity();

            var progress = new Progress<double>(p => OperationProgress = p);
            await _fileManager.ExportFileAsync(SelectedVault.Id, args.entryId, args.destinationPath, progress);

            await _logService.LogEventAsync(ActivityEventType.FileExported, ActivitySeverity.Info, SelectedVault.Name, $"File exported to disk.");
            await RefreshActivityLogsAsync();

            ShowStatus("File decrypted and verified successfully.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"Export failed: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task DeleteFileAsync(Guid entryId)
    {
        if (SelectedVault == null || !IsUnlocked) return;

        try
        {
            await _fileManager.DeleteEntryAsync(SelectedVault.Id, entryId);
            var entry = Entries.FirstOrDefault(e => e.Id == entryId);
            if (entry != null)
            {
                Entries.Remove(entry);
                SelectedVault.FileCount = Math.Max(0, SelectedVault.FileCount - 1);
            }

            await _logService.LogEventAsync(ActivityEventType.FileDeleted, ActivitySeverity.Info, SelectedVault.Name, "File removed from vault.");
            await RefreshActivityLogsAsync();

            ShowStatus("File removed from vault.", InfoBarSeverity.Informational);
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to delete file: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    [RelayCommand]
    public async Task AnalyzeCarrierAsync(string carrierPath)
    {
        try
        {
            StegoCarrierPath = carrierPath;
            StegoCarrierInfo = await _stegoService.AnalyzeCarrierAsync(carrierPath);
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to analyze carrier: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    [RelayCommand]
    public async Task EmbedStegoAsync()
    {
        if (string.IsNullOrEmpty(StegoCarrierPath) || string.IsNullOrEmpty(StegoPayloadPath) || string.IsNullOrEmpty(StegoPassword))
        {
            ShowStatus("Carrier, payload, and password are required.", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            IsBusy = true;
            BusyStatusText = "Concealing encrypted payload in carrier...";
            OperationProgress = 0;
            _policyService.RegisterUserActivity();

            string ext = Path.GetExtension(StegoCarrierPath);
            string outPath = string.IsNullOrEmpty(StegoOutputPath) 
                ? Path.Combine(Path.GetDirectoryName(StegoCarrierPath)!, $"{Path.GetFileNameWithoutExtension(StegoCarrierPath)}_concealed{ext}") 
                : StegoOutputPath;

            var progress = new Progress<double>(p => OperationProgress = p);
            await _stegoService.EmbedPayloadAsync(StegoCarrierPath, StegoPayloadPath, StegoPassword, outPath, progress);

            await _logService.LogEventAsync(ActivityEventType.StegoPayloadEmbedded, ActivitySeverity.Info, "STEGO", $"Payload embedded into {Path.GetFileName(outPath)}");
            await RefreshActivityLogsAsync();

            ShowStatus($"Concealed image saved to: {outPath}", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"Stego embedding failed: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ExtractStegoAsync()
    {
        if (string.IsNullOrEmpty(StegoExtractionCarrierPath) || string.IsNullOrEmpty(StegoExtractionPassword))
        {
            ShowStatus("Carrier file and password are required.", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            IsBusy = true;
            BusyStatusText = "Extracting and decrypting hidden payload...";
            OperationProgress = 0;
            _policyService.RegisterUserActivity();

            string outDir = string.IsNullOrEmpty(StegoExtractionOutDir) 
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") 
                : StegoExtractionOutDir;

            var progress = new Progress<double>(p => OperationProgress = p);
            var result = await _stegoService.ExtractPayloadAsync(StegoExtractionCarrierPath, StegoExtractionPassword, outDir, progress);

            await _logService.LogEventAsync(ActivityEventType.StegoPayloadExtracted, ActivitySeverity.Info, "STEGO", $"Payload '{result.OriginalFileName}' extracted.");
            await RefreshActivityLogsAsync();

            ShowStatus($"Extracted and verified payload: {result.OriginalFileName} ({result.PayloadSizeBytes:N0} bytes)", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"Stego extraction failed: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task CreateBackupAsync(string destinationPath)
    {
        if (SelectedVault == null) return;

        try
        {
            IsBusy = true;
            BusyStatusText = "Creating encrypted backup package...";
            OperationProgress = 0;
            _policyService.RegisterUserActivity();

            var progress = new Progress<double>(p => OperationProgress = p);
            await _backupService.CreateBackupAsync(SelectedVault.Id, destinationPath, progress);

            await _logService.LogEventAsync(ActivityEventType.BackupCreated, ActivitySeverity.Info, SelectedVault.Name, "Encrypted backup created.");
            await RefreshActivityLogsAsync();

            ShowStatus($"Encrypted backup saved to {destinationPath}", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"Backup failed: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ValidateBackupArchiveAsync(string archivePath)
    {
        try
        {
            IsBusy = true;
            BusyStatusText = "Verifying backup archive checksums...";
            _policyService.RegisterUserActivity();

            InspectedBackupPath = archivePath;
            InspectedManifest = await _backupService.ValidateBackupAsync(archivePath);

            ShowStatus("Backup archive integrity verified: 100% valid.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"Backup verification failed: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task RestoreBackupAsync((string archivePath, string targetDir, string? newName) args)
    {
        try
        {
            IsBusy = true;
            BusyStatusText = "Restoring backup archive into vault...";
            _policyService.RegisterUserActivity();

            var restored = await _backupService.RestoreBackupAsync(args.archivePath, args.targetDir, args.newName);
            Vaults.Add(restored);
            SelectedVault = restored;

            await _logService.LogEventAsync(ActivityEventType.BackupRestored, ActivitySeverity.Info, restored.Name, "Backup restored.");
            await RefreshActivityLogsAsync();

            ShowStatus($"Vault '{restored.Name}' restored from backup. Enter master password to unlock.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"Restoration failed: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task VerifyAllVaultFilesIntegrityAsync()
    {
        if (SelectedVault == null || !IsUnlocked) return;

        try
        {
            IsBusy = true;
            BusyStatusText = "Authenticating all chunk cryptographic tags...";
            _policyService.RegisterUserActivity();

            bool valid = await _vaultRepo.VerifyVaultIntegrityAsync(SelectedVault.Id);
            if (valid)
            {
                LastIntegrityCheckResult = $"Verified {DateTime.Now:g}: All files intact.";
                await _logService.LogEventAsync(ActivityEventType.IntegrityCheckPassed, ActivitySeverity.Info, SelectedVault.Name, "Full vault integrity check passed.");
                ShowStatus("Integrity Verification Passed: 0 tampered chunks found.", InfoBarSeverity.Success);
            }
            else
            {
                LastIntegrityCheckResult = $"FAILED {DateTime.Now:g}: Tampered ciphertext detected!";
                await _logService.LogEventAsync(ActivityEventType.IntegrityCheckFailed, ActivitySeverity.SecurityAlert, SelectedVault.Name, "Integrity check failed: tampered data detected!");
                ShowStatus("ALERT: Vault integrity check failed. Some files may be corrupted or altered.", InfoBarSeverity.Error);
            }

            await RefreshActivityLogsAsync();
        }
        catch (Exception ex)
        {
            ShowStatus($"Verification error: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task RefreshActivityLogsAsync()
    {
        try
        {
            var logs = await _logService.GetLogsAsync();
            ActivityLogs.Clear();
            foreach (var l in logs)
            {
                ActivityLogs.Add(l);
            }
        }
        catch { }
    }

    [RelayCommand]
    public async Task ClearActivityLogsAsync()
    {
        try
        {
            await _logService.ClearLogsAsync();
            ActivityLogs.Clear();
            ShowStatus("Local activity history cleared.", InfoBarSeverity.Informational);
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to clear logs: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    public PasswordStrengthResult EvaluatePassword(string password)
    {
        return _passwordValidator.EvaluatePassword(password);
    }

    public void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusMessage = message;
        StatusSeverity = severity;
        IsStatusOpen = true;
    }
}
