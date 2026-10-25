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
    private readonly ISecureShredderService _shredderService;
    private readonly IWindowsHelloService _windowsHelloService;
    private readonly ISoundEffectService _soundService;
    private readonly IDecoyVaultService _decoyService;

    public MainViewModel(
        IVaultRepository vaultRepo,
        IVaultFileManager fileManager,
        ISteganographyService stegoService,
        IBackupService backupService,
        IActivityLogService logService,
        ISecurityPolicyService policyService,
        IPasswordPolicyValidator passwordValidator,
        ISecureShredderService shredderService,
        IWindowsHelloService windowsHelloService,
        ISoundEffectService soundService,
        IDecoyVaultService decoyService)
    {
        _vaultRepo = vaultRepo;
        _fileManager = fileManager;
        _stegoService = stegoService;
        _backupService = backupService;
        _logService = logService;
        _policyService = policyService;
        _passwordValidator = passwordValidator;
        _shredderService = shredderService;
        _windowsHelloService = windowsHelloService;
        _soundService = soundService;
        _decoyService = decoyService;

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

        _ = CheckWindowsHelloSupportAsync();
    }

    public ISoundEffectService SoundService => _soundService;
    public IDecoyVaultService DecoyService => _decoyService;

    [ObservableProperty]
    private bool _isSoundEffectsEnabled = true;

    [ObservableProperty]
    private bool _hasDuressPassword;

    partial void OnIsSoundEffectsEnabledChanged(bool value)
    {
        _soundService.IsSoundEnabled = value;
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

    public event Action<string>? NavigationRequested;

    [ObservableProperty]
    private int _totalVaultsCount;

    [ObservableProperty]
    private int _lockedVaultsCount;

    [ObservableProperty]
    private int _unlockedVaultsCount;

    [ObservableProperty]
    private string _totalEncryptedStorageText = "0 KB";

    [ObservableProperty]
    private string _lastBackupDateText = "None Recorded";

    [ObservableProperty]
    private ObservableCollection<string> _securityNotices = new();

    [ObservableProperty]
    private ObservableCollection<ActivityLogRecord> _filteredActivityLogs = new();

    [ObservableProperty]
    private string _selectedLogCategory = "All";

    [ObservableProperty]
    private string _logSearchQuery = string.Empty;

    // Emergency Access & Policy State
    [ObservableProperty]
    private bool _isAutoLockEnabled = true;

    [ObservableProperty]
    private int _inactivityTimeoutMinutes = 15;

    [ObservableProperty]
    private bool _isSessionLockEnabled = true;

    [ObservableProperty]
    private bool _isDeadMansEnabled = false;

    [ObservableProperty]
    private int _deadMansTimeoutMinutes = 60;

    [ObservableProperty]
    private int _warningCountdownSeconds = 30;

    [ObservableProperty]
    private string _emergencyPolicyStatusDescription = "Standard Protection Active";

    // Stego UI State & Capacity Check
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
    private bool _doesPayloadFit = true;

    [ObservableProperty]
    private string _capacityStatusMessage = "Carrier ready for payload selection.";

    [ObservableProperty]
    private long _payloadSizeBytes;

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
            RefreshDashboardStats();
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

            // Plausible Deniability Check (Duress / Decoy Mode)
            if (await _decoyService.VerifyIsDuressPasswordAsync(SelectedVault.Id, password))
            {
                var decoyVault = await _decoyService.EnsureDecoyVaultAsync(SelectedVault.Id, password);
                SelectedVault.State = VaultState.Unlocked;
                UpdateLockState();

                var decoyEntries = await _fileManager.GetEntriesAsync(decoyVault.Id);
                Entries.Clear();
                foreach (var entry in decoyEntries) Entries.Add(entry);

                _soundService.PlayUnlockSound();
                await _logService.LogEventAsync(ActivityEventType.DuressUnlockTriggered, ActivitySeverity.SecurityAlert, SelectedVault.Name, "Coercion alert: Duress password entered. Decoy vault unsealed seamlessly.");
                await RefreshActivityLogsAsync();

                ShowStatus($"Vault '{SelectedVault.Name}' unlocked.", InfoBarSeverity.Success);
                return;
            }

            await _vaultRepo.UnlockVaultAsync(SelectedVault.Id, password);
            SelectedVault.State = VaultState.Unlocked;
            UpdateLockState();

            await LoadEntriesAsync();
            _soundService.PlayUnlockSound();
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

            _soundService.PlayLockSound();
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

            _soundService.PlayEmergencySound();
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

    [ObservableProperty]
    private bool _isWindowsHelloAvailable;

    [ObservableProperty]
    private string _currentSortOrder = "DateDesc";

    public async Task CheckWindowsHelloSupportAsync()
    {
        IsWindowsHelloAvailable = await _windowsHelloService.IsAvailableAsync();
    }

    public async Task<bool> VerifyWindowsHelloConsentAsync(string prompt = "Authenticate to confirm sensitive action in PHANTOMVAULT")
    {
        return await _windowsHelloService.VerifyUserAsync(prompt);
    }

    [RelayCommand]
    public async Task ImportFileAsync(string filePath)
    {
        await ImportFileWithShredOptionAsync(filePath, false);
    }

    public async Task ImportFileWithShredOptionAsync(string filePath, bool shredOriginal)
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

            if (shredOriginal && File.Exists(filePath))
            {
                BusyStatusText = $"Securely shredding plaintext original (DoD 5220.22-M)...";
                await _shredderService.ShredFileAsync(filePath, 3, progress);
                _soundService.PlayShredSound();
                await _logService.LogEventAsync(ActivityEventType.FilePurged, ActivitySeverity.Warning, SelectedVault.Name, $"Plaintext original securely destroyed: {Path.GetFileName(filePath)}");
                ShowStatus($"Encrypted '{entry.Name}' & securely shredded original from disk.", InfoBarSeverity.Success);
            }
            else
            {
                ShowStatus($"File '{entry.Name}' encrypted and stored in vault.", InfoBarSeverity.Success);
            }

            await RefreshActivityLogsAsync();
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

    public async Task<byte[]?> GetDecryptedBytesAsync(Guid fileEntryId)
    {
        if (SelectedVault == null || !IsUnlocked) return null;

        try
        {
            IsBusy = true;
            BusyStatusText = "Decrypting file directly to secure memory buffer...";
            OperationProgress = 0;
            _policyService.RegisterUserActivity();

            var progress = new Progress<double>(p => OperationProgress = p);
            return await _fileManager.GetDecryptedFileBytesAsync(SelectedVault.Id, fileEntryId, progress);
        }
        catch (Exception ex)
        {
            ShowStatus($"In-memory preview failed: {ex.Message}", InfoBarSeverity.Error);
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ShredExternalFileAsync(string filePath)
    {
        if (!File.Exists(filePath)) return;

        try
        {
            IsBusy = true;
            string fileName = Path.GetFileName(filePath);
            BusyStatusText = $"Shredding '{fileName}' with 3-pass DoD 5220.22-M overwrite...";
            OperationProgress = 0;
            _policyService.RegisterUserActivity();

            var progress = new Progress<double>(p => OperationProgress = p);
            await _shredderService.ShredFileAsync(filePath, 3, progress);
            _soundService.PlayShredSound();

            await _logService.LogEventAsync(ActivityEventType.FilePurged, ActivitySeverity.Warning, "SYSTEM", $"External file securely shredded: {fileName}");
            await RefreshActivityLogsAsync();

            ShowStatus($"File '{fileName}' was permanently shredded from disk.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"Shredding failed: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void SortEntries(string sortMode)
    {
        CurrentSortOrder = sortMode;
        if (Entries.Count <= 1) return;

        var list = Entries.ToList();
        switch (sortMode)
        {
            case "NameAsc":
                list = list.OrderBy(e => e.Name).ToList();
                break;
            case "NameDesc":
                list = list.OrderByDescending(e => e.Name).ToList();
                break;
            case "SizeDesc":
                list = list.OrderByDescending(e => e.PlaintextSizeBytes).ToList();
                break;
            case "SizeAsc":
                list = list.OrderBy(e => e.PlaintextSizeBytes).ToList();
                break;
            case "DateDesc":
                list = list.OrderByDescending(e => e.CreatedAtUtc).ToList();
                break;
            case "DateAsc":
                list = list.OrderBy(e => e.CreatedAtUtc).ToList();
                break;
        }

        Entries.Clear();
        foreach (var item in list)
        {
            Entries.Add(item);
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
            _soundService.PlayStegoSound();

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
            _soundService.PlayStegoSound();

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
            ApplyLogFilter();
            RefreshDashboardStats();
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
            FilteredActivityLogs.Clear();
            RefreshDashboardStats();
            ShowStatus("Local activity history cleared.", InfoBarSeverity.Informational);
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to clear logs: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    public void RefreshDashboardStats()
    {
        TotalVaultsCount = Vaults.Count;
        LockedVaultsCount = Vaults.Count(v => v.State == VaultState.Locked);
        UnlockedVaultsCount = Vaults.Count(v => v.State == VaultState.Unlocked);

        long totalCipherBytes = Vaults.Sum(v => v.TotalCiphertextBytes);
        TotalEncryptedStorageText = FormatBytes(totalCipherBytes);

        var lastBackupLog = ActivityLogs.FirstOrDefault(l => l.EventType == ActivityEventType.BackupCreated);
        LastBackupDateText = lastBackupLog != null ? lastBackupLog.TimestampUtc.ToLocalTime().ToString("g") : "None Recorded";

        SecurityNotices.Clear();
        if (Vaults.Count == 0)
        {
            SecurityNotices.Add("No encrypted vaults created. Initialize your first vault to begin.");
        }
        else if (UnlockedVaultsCount == 0)
        {
            SecurityNotices.Add("All vaults are currently locked and keys are zeroized in memory.");
        }

        if (lastBackupLog == null && Vaults.Count > 0)
        {
            SecurityNotices.Add("Notice: No backup archive (.pvbackup) has been exported for your vaults yet.");
        }

        if (IsDeadMansEnabled)
        {
            SecurityNotices.Add($"Dead-Man's Switch is active ({DeadMansTimeoutMinutes} min inactivity timeout).");
        }
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F2} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }

    public void ApplyLogFilter()
    {
        FilteredActivityLogs.Clear();
        foreach (var log in ActivityLogs)
        {
            bool matchesCategory = SelectedLogCategory switch
            {
                "All" => true,
                "Vault" => log.EventType is ActivityEventType.VaultCreated or ActivityEventType.VaultUnlocked or ActivityEventType.VaultLocked or ActivityEventType.PasswordChanged,
                "File" => log.EventType is ActivityEventType.FileImported or ActivityEventType.FileExported or ActivityEventType.FileDeleted or ActivityEventType.FileRenamed or ActivityEventType.FilePurged,
                "Backup" => log.EventType is ActivityEventType.BackupCreated or ActivityEventType.BackupRestored,
                "Security" => log.EventType is ActivityEventType.IntegrityCheckPassed or ActivityEventType.IntegrityCheckFailed or ActivityEventType.SecuritySettingsChanged or ActivityEventType.EmergencyLockTriggered,
                "Stego" => log.EventType is ActivityEventType.StegoPayloadEmbedded or ActivityEventType.StegoPayloadExtracted,
                "Errors" => log.Severity is ActivitySeverity.Error or ActivitySeverity.SecurityAlert or ActivitySeverity.Warning,
                _ => true
            };

            bool matchesSearch = string.IsNullOrWhiteSpace(LogSearchQuery) ||
                log.Message.Contains(LogSearchQuery, StringComparison.OrdinalIgnoreCase) ||
                log.VaultName.Contains(LogSearchQuery, StringComparison.OrdinalIgnoreCase) ||
                log.EventType.ToString().Contains(LogSearchQuery, StringComparison.OrdinalIgnoreCase);

            if (matchesCategory && matchesSearch)
            {
                FilteredActivityLogs.Add(log);
            }
        }
    }

    partial void OnSelectedLogCategoryChanged(string value) => ApplyLogFilter();
    partial void OnLogSearchQueryChanged(string value) => ApplyLogFilter();

    public void RequestNavigate(string targetPage)
    {
        NavigationRequested?.Invoke(targetPage);
    }

    public void UpdateStegoCapacityStatus()
    {
        if (StegoCarrierInfo == null)
        {
            CapacityStatusMessage = "Please select a PNG carrier image or WAV audio file first.";
            DoesPayloadFit = false;
            return;
        }

        if (string.IsNullOrEmpty(StegoPayloadPath) || !File.Exists(StegoPayloadPath))
        {
            CapacityStatusMessage = $"Carrier ready. Usable capacity: {FormatBytes(StegoCarrierInfo.UsableCapacityBytes)}.";
            DoesPayloadFit = true;
            return;
        }

        var fi = new FileInfo(StegoPayloadPath);
        PayloadSizeBytes = fi.Length;
        long requiredBytes = PayloadSizeBytes + 100;

        if (requiredBytes <= StegoCarrierInfo.UsableCapacityBytes)
        {
            DoesPayloadFit = true;
            CapacityStatusMessage = $"Payload fits! ({FormatBytes(PayloadSizeBytes)} of {FormatBytes(StegoCarrierInfo.UsableCapacityBytes)} usable).";
        }
        else
        {
            DoesPayloadFit = false;
            CapacityStatusMessage = $"Payload is too large for this carrier. Required: {FormatBytes(requiredBytes)}, Available: {FormatBytes(StegoCarrierInfo.UsableCapacityBytes)}.";
        }
    }

    [RelayCommand]
    public async Task RenameEntryAsync((Guid entryId, string newName) args)
    {
        if (SelectedVault == null || !IsUnlocked) return;

        try
        {
            await _fileManager.RenameEntryAsync(SelectedVault.Id, args.entryId, args.newName);
            var entry = Entries.FirstOrDefault(e => e.Id == args.entryId);
            if (entry != null)
            {
                entry.Name = args.newName;
            }

            await _logService.LogEventAsync(ActivityEventType.FileRenamed, ActivitySeverity.Info, SelectedVault.Name, $"Renamed file to: {args.newName}");
            await RefreshActivityLogsAsync();
            ShowStatus($"File renamed to '{args.newName}'.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to rename file: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    [RelayCommand]
    public void SaveEmergencyPolicy()
    {
        var policy = new EmergencyPolicy
        {
            AutoLockOnInactivity = IsAutoLockEnabled,
            InactivityMinutes = InactivityTimeoutMinutes,
            LockOnWorkstationLock = IsSessionLockEnabled,
            DeadMansSwitchEnabled = IsDeadMansEnabled,
            DeadMansSwitchTimeoutMinutes = DeadMansTimeoutMinutes,
            WarningCountdownSeconds = WarningCountdownSeconds
        };

        _policyService.UpdatePolicy(policy);
        EmergencyPolicyStatusDescription = IsDeadMansEnabled 
            ? $"Dead-Man's Switch Active ({DeadMansTimeoutMinutes}m timeout, {WarningCountdownSeconds}s warning)"
            : (IsAutoLockEnabled ? $"Auto-Lock Active ({InactivityTimeoutMinutes}m timeout)" : "Inactivity Protection Disabled");

        _ = _logService.LogEventAsync(ActivityEventType.SecuritySettingsChanged, ActivitySeverity.Info, "GLOBAL", "Emergency & lock policies updated.");
        _ = RefreshActivityLogsAsync();
        ShowStatus("Emergency and security policies applied successfully.", InfoBarSeverity.Success);
    }

    [RelayCommand]
    public void TestEmergencyCountdown()
    {
        _policyService.RegisterUserActivity();
        DeadMansCountdownSeconds = 10;
        IsDeadMansWarningVisible = true;
        ShowStatus("Test Mode: 10-second inactivity warning triggered. Click 'Dismiss / Still Active' to cancel.", InfoBarSeverity.Informational);
    }

    [RelayCommand]
    public void CancelEmergencyAction()
    {
        _policyService.RegisterUserActivity();
        IsDeadMansWarningVisible = false;
        DeadMansCountdownSeconds = 0;
        ShowStatus("Pending emergency action safely cancelled.", InfoBarSeverity.Informational);
    }

    public async Task SetupDuressPasswordAsync(string duressPassword)
    {
        if (SelectedVault == null) return;
        try
        {
            IsBusy = true;
            BusyStatusText = "Configuring plausible deniability decoy enclave...";
            _policyService.RegisterUserActivity();

            await _decoyService.SetDuressPasswordAsync(SelectedVault.Id, duressPassword);
            HasDuressPassword = true;
            _soundService.PlayStegoSound();
            await _logService.LogEventAsync(ActivityEventType.DecoyVaultCreated, ActivitySeverity.Info, SelectedVault.Name, "Plausible deniability decoy enclave configured.");
            await RefreshActivityLogsAsync();
            ShowStatus("Plausible deniability configured. Entering this duress password will seamlessly unseal the decoy enclave.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to configure duress password: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RefreshDuressStatusAsync()
    {
        if (SelectedVault != null)
        {
            HasDuressPassword = await _decoyService.HasDuressPasswordAsync(SelectedVault.Id);
        }
        else
        {
            HasDuressPassword = false;
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
