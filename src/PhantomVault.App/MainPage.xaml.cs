using System;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PhantomVault.Core.Interfaces;
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
        Loaded += MainPage_Loaded;
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        await _vm.InitializeAsync();

        // Default navigation to VaultView
        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(VaultView));

        UpdateStatusBadge();

        // If no vaults exist, prompt First-Run Setup Wizard
        if (_vm.Vaults.Count == 0)
        {
            await ShowFirstRunWizardAsync();
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
                    DeadMansCountdownText.Text = $"INACTIVITY WARNING: Vaults locking in {_vm.DeadMansCountdownSeconds} seconds!";
                    break;

                case nameof(_vm.IsUnlocked):
                    UpdateStatusBadge();
                    break;
            }
        });
    }

    private void UpdateStatusBadge()
    {
        if (_vm.IsUnlocked)
        {
            VaultStatusPill.Background = (Brush)Application.Current.Resources["BrandSuccessBrush"];
            VaultStatusText.Text = "UNLOCKED";
            VaultStatusText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
        }
        else
        {
            VaultStatusPill.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 55, 65, 81));
            VaultStatusText.Text = "LOCKED";
            VaultStatusText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 156, 163, 175));
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            _policyService.RegisterUserActivity();

            string? tag = item.Tag?.ToString();
            Type targetPage = tag switch
            {
                "Vault" => typeof(VaultView),
                "Stego" => typeof(StegoView),
                "Backup" => typeof(BackupView),
                "Security" => typeof(SecurityCenterView),
                "Logs" => typeof(ActivityLogView),
                "Settings" => typeof(SettingsView),
                _ => typeof(VaultView)
            };

            ContentFrame.Navigate(targetPage);
        }
    }

    private async void EmergencyLockAll_Click(object sender, RoutedEventArgs e)
    {
        await _vm.LockAllVaultsAsync();
    }

    private void DismissDeadMans_Click(object sender, RoutedEventArgs e)
    {
        _policyService.RegisterUserActivity();
    }

    private async System.Threading.Tasks.Task ShowFirstRunWizardAsync()
    {
        var welcomeText = new TextBlock 
        { 
            Text = "Welcome to PHANTOMVAULT.\n\nPhilosophy:\n• Privacy by Design: All data encrypted at rest with AES-256-GCM.\n• Local-First: 100% offline. Zero telemetry, zero cloud backdoors.\n• Non-Recoverable Secrets: If you lose your master password, no recovery is possible.\n\nLet's create your first encrypted vault:",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };

        var nameBox = new TextBox { PlaceholderText = "Vault Name (e.g. PersonalVault)", Margin = new Thickness(0, 0, 0, 8) };
        var passBox = new PasswordBox { PlaceholderText = "Master Password (min 12 chars recommended)", Margin = new Thickness(0, 0, 0, 8) };
        var confirmBox = new PasswordBox { PlaceholderText = "Confirm Master Password", Margin = new Thickness(0, 0, 0, 8) };

        var stack = new StackPanel
        {
            Spacing = 4,
            Children = { welcomeText, nameBox, passBox, confirmBox }
        };

        var dialog = new ContentDialog
        {
            Title = "PHANTOMVAULT First-Run Setup",
            Content = stack,
            PrimaryButtonText = "Initialize First Vault",
            CloseButtonText = "Skip For Now",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            string name = string.IsNullOrWhiteSpace(nameBox.Text) ? "DefaultVault" : nameBox.Text;
            if (string.IsNullOrEmpty(passBox.Password) || passBox.Password != confirmBox.Password)
            {
                _vm.ShowStatus("Passwords do not match or are empty. Vault not created.", InfoBarSeverity.Warning);
                return;
            }

            string baseDir = PhantomVault.Storage.StoragePaths.DefaultVaultsDirectory;
            await _vm.CreateNewVaultAsync((name, baseDir, passBox.Password, PhantomVault.Core.Enums.KdfAlgorithm.Argon2id));
            ContentFrame.Navigate(typeof(VaultView));
        }
    }
}
