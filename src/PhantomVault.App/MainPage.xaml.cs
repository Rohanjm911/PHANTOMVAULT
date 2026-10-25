using System;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PhantomVault.Core.Interfaces;
using PhantomVault.Storage;
using PhantomVault_App.Helpers;
using PhantomVault_App.ViewModels;
using PhantomVault_App.Views;

namespace PhantomVault_App;

public sealed partial class MainPage : Page
{
    private readonly MainViewModel _vm;
    private readonly ISecurityPolicyService _policyService;

    public MainPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<MainViewModel>();
        _policyService = App.Services.GetRequiredService<ISecurityPolicyService>();

        _vm.PropertyChanged += Vm_PropertyChanged;
        _vm.NavigationRequested += OnNavigationRequested;
        Loaded += MainPage_Loaded;
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _vm.InitializeAsync();

            NavView.SelectedItem = NavView.MenuItems[0];
            ContentFrame.Navigate(typeof(DashboardView));
            UpdateStatusBadge();

            if (_vm.Vaults.Count == 0 && this.XamlRoot != null)
            {
                await ShowFirstRunWizardAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MAINPAGE INIT ERROR] {ex}");
        }
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(_vm.IsStatusOpen):
                case nameof(_vm.StatusMessage):
                case nameof(_vm.StatusSeverity):
                    AppInfoBar.Message = _vm.StatusMessage;
                    AppInfoBar.Severity = _vm.StatusSeverity;
                    AppInfoBar.IsOpen = _vm.IsStatusOpen;
                    break;

                case nameof(_vm.IsBusy):
                case nameof(_vm.BusyStatusText):
                case nameof(_vm.OperationProgress):
                    BusyOverlay.Visibility = _vm.IsBusy ? Visibility.Visible : Visibility.Collapsed;
                    BusyText.Text = _vm.BusyStatusText;
                    BusyProgressBar.Value = _vm.OperationProgress;
                    break;

                case nameof(_vm.IsDeadMansWarningVisible):
                case nameof(_vm.DeadMansCountdownSeconds):
                    DeadMansBanner.Visibility = _vm.IsDeadMansWarningVisible ? Visibility.Visible : Visibility.Collapsed;
                    DeadMansCountdownText.Text = $"INACTIVITY WARNING: All vaults locking in {_vm.DeadMansCountdownSeconds} seconds!";
                    break;

                case nameof(_vm.IsUnlocked):
                case nameof(_vm.SelectedVault):
                    UpdateStatusBadge();
                    break;
            }
        });
    }

    private void UpdateStatusBadge()
    {
        HeaderActiveVaultText.Text = _vm.SelectedVault != null ? _vm.SelectedVault.Name : "None Selected";

        if (_vm.IsUnlocked)
        {
            VaultStatusPill.Background = (Brush)Application.Current.Resources["SuccessBrush"];
            VaultStatusPill.BorderBrush = (Brush)Application.Current.Resources["SuccessBrush"];
            VaultStatusText.Text = "UNLOCKED";
            VaultStatusText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 11, 13, 18));
        }
        else
        {
            VaultStatusPill.Background = (Brush)Application.Current.Resources["ElevatedSurfaceBrush"];
            VaultStatusPill.BorderBrush = (Brush)Application.Current.Resources["BorderBrush"];
            VaultStatusText.Text = "LOCKED";
            VaultStatusText.Foreground = (Brush)Application.Current.Resources["MutedTextBrush"];
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            _policyService.RegisterUserActivity();

            string? tag = item.Tag?.ToString();
            NavigateToTag(tag);
        }
    }

    private void OnNavigationRequested(string targetTag)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            foreach (var menuItem in NavView.MenuItems)
            {
                if (menuItem is NavigationViewItem navItem && navItem.Tag?.ToString() == targetTag)
                {
                    NavView.SelectedItem = navItem;
                    NavigateToTag(targetTag);
                    break;
                }
            }
        });
    }

    private void NavigateToTag(string? tag)
    {
        Type targetPage = tag switch
        {
            "Dashboard" => typeof(DashboardView),
            "Vaults" => typeof(VaultsView),
            "Vault" => typeof(VaultView),
            "Stego" => typeof(StegoView),
            "Backup" => typeof(BackupView),
            "Security" => typeof(SecurityCenterView),
            "Emergency" => typeof(EmergencyAccessView),
            "Logs" => typeof(ActivityLogView),
            "UserGuide" => typeof(UserGuideView),
            "Settings" => typeof(SettingsView),
            _ => typeof(DashboardView)
        };

        ContentFrame.Navigate(targetPage);
    }

    private async void EmergencyLockAll_Click(object sender, RoutedEventArgs e)
    {
        await _vm.LockAllVaultsAsync();
    }

    private void DismissDeadMans_Click(object sender, RoutedEventArgs e)
    {
        _policyService.RegisterUserActivity();
        _vm.CancelEmergencyAction();
    }

    private async System.Threading.Tasks.Task ShowFirstRunWizardAsync()
    {
        var titleText = new TextBlock
        {
            Text = "Trust Nothing. Reveal Nothing.",
            FontStyle = Windows.UI.Text.FontStyle.Italic,
            FontSize = 13,
            Foreground = (Brush)Application.Current.Resources["PrimaryAccentBrush"],
            Margin = new Thickness(0, 0, 0, 8)
        };

        var welcomeText = new TextBlock 
        { 
            Text = "Welcome to PHANTOMVAULT, your private workspace for encrypted files and concealed payloads.\n\n" +
                   "Core Security Philosophy:\n" +
                   "• Local-First & 100% Offline: Zero telemetry, zero cloud backdoors, zero remote reporting.\n" +
                   "• Standard Cryptography: AES-256-GCM authenticated cipher with Argon2id memory-hard key derivation.\n" +
                   "• Non-Recoverable Secrets: There are no master keys, reset links, or recovery backdoors. If you lose your master password, your files cannot be decrypted.\n\n" +
                   "Create your first local vault to get started:",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Foreground = (Brush)Application.Current.Resources["SecondaryTextBrush"],
            Margin = new Thickness(0, 0, 0, 14)
        };

        var nameBox = new TextBox 
        { 
            Header = "First Vault Name", 
            PlaceholderText = "e.g., PersonalVault or WorkEnclave", 
            Text = "PersonalVault",
            Margin = new Thickness(0, 0, 0, 10) 
        };

        var passBox = new PasswordBox 
        { 
            Header = "Master Password", 
            PlaceholderText = "Enter a strong master password (12+ chars recommended)", 
            Margin = new Thickness(0, 0, 0, 10) 
        };

        var confirmBox = new PasswordBox 
        { 
            Header = "Confirm Master Password", 
            PlaceholderText = "Re-enter master password", 
            Margin = new Thickness(0, 0, 0, 10) 
        };

        var stack = new StackPanel
        {
            Width = 460,
            Spacing = 4,
            Children = { titleText, welcomeText, nameBox, passBox, confirmBox }
        };

        var dialog = new ContentDialog
        {
            Title = "PHANTOMVAULT First-Run Setup",
            Content = stack,
            PrimaryButtonText = "Create Your First Vault",
            SecondaryButtonText = "Open Existing Vault...",
            CloseButtonText = "Skip Setup",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            string name = string.IsNullOrWhiteSpace(nameBox.Text) ? "PersonalVault" : nameBox.Text.Trim();
            if (string.IsNullOrEmpty(passBox.Password) || passBox.Password != confirmBox.Password)
            {
                _vm.ShowStatus("Passwords do not match or are empty. Vault not created.", InfoBarSeverity.Warning);
                return;
            }

            string baseDir = StoragePaths.DefaultVaultsDirectory;
            await _vm.CreateNewVaultAsync((name, baseDir, passBox.Password, PhantomVault.Core.Enums.KdfAlgorithm.Argon2id));
            OnNavigationRequested("Vault");
        }
        else if (result == ContentDialogResult.Secondary)
        {
            string? folder = await FilePickerHelper.PickSingleFolderAsync();
            if (!string.IsNullOrEmpty(folder))
            {
                try
                {
                    var vaultRepo = App.Services.GetRequiredService<IVaultRepository>();
                    var descriptor = await vaultRepo.OpenVaultAsync(folder);
                    _vm.Vaults.Add(descriptor);
                    _vm.SelectedVault = descriptor;
                    _vm.ShowStatus($"Opened existing vault: {descriptor.Name}", InfoBarSeverity.Success);
                    OnNavigationRequested("Vault");
                }
                catch (Exception ex)
                {
                    _vm.ShowStatus($"Failed to open vault: {ex.Message}", InfoBarSeverity.Error);
                }
            }
        }
    }
}
