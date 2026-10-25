using System;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PhantomVault.Core.Enums;
using PhantomVault.Core.Interfaces;
using PhantomVault.Core.Models;
using PhantomVault_App.Helpers;
using PhantomVault_App.ViewModels;

namespace PhantomVault_App.Views;

public sealed partial class VaultsView : Page
{
    private readonly MainViewModel _vm;
    private readonly IVaultRepository _vaultRepo;

    public VaultsView()
    {
        _vm = App.Services.GetRequiredService<MainViewModel>();
        _vaultRepo = App.Services.GetRequiredService<IVaultRepository>();
        InitializeComponent();
        _vm.PropertyChanged += Vm_PropertyChanged;
        Loaded += VaultsView_Loaded;
    }

    public static Visibility GetUnlockVisibility(VaultState state) => state == VaultState.Unlocked ? Visibility.Collapsed : Visibility.Visible;
    public static Visibility GetLockVisibility(VaultState state) => state == VaultState.Unlocked ? Visibility.Visible : Visibility.Collapsed;

    private void VaultsView_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateUI();
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_vm.Vaults) || e.PropertyName == nameof(_vm.SelectedVault) || e.PropertyName == nameof(_vm.IsUnlocked))
        {
            DispatcherQueue.TryEnqueue(UpdateUI);
        }
    }

    private void UpdateUI()
    {
        foreach (var v in _vm.Vaults)
        {
            v.State = _vaultRepo.IsVaultUnlocked(v.Id) ? VaultState.Unlocked : VaultState.Locked;
        }

        VaultsItemsControl.ItemsSource = null;
        VaultsItemsControl.ItemsSource = _vm.Vaults;
        EmptyVaultsBorder.Visibility = _vm.Vaults.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await _vm.LoadVaultsAsync();
    }

    private async void CreateVaultButton_Click(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox 
        { 
            Header = "Vault Name", 
            PlaceholderText = "e.g., FinancialRecords or SecureWorkspace", 
            Margin = new Thickness(0, 0, 0, 10) 
        };

        var pathBox = new TextBox 
        { 
            Header = "Storage Location", 
            Text = PhantomVault.Storage.StoragePaths.DefaultVaultsDirectory, 
            IsReadOnly = true,
            Margin = new Thickness(0, 0, 0, 10) 
        };

        var browseButton = new Button { Content = "Browse Folder...", Margin = new Thickness(0, 0, 0, 14) };
        browseButton.Click += async (s, a) =>
        {
            var folder = await FilePickerHelper.PickSingleFolderAsync();
            if (!string.IsNullOrEmpty(folder))
            {
                pathBox.Text = folder;
            }
        };

        var passBox = new PasswordBox 
        { 
            Header = "Master Password", 
            PlaceholderText = "Minimum 12 characters recommended", 
            Margin = new Thickness(0, 0, 0, 10) 
        };

        var confirmBox = new PasswordBox 
        { 
            Header = "Confirm Master Password", 
            PlaceholderText = "Re-enter master password", 
            Margin = new Thickness(0, 0, 0, 10) 
        };

        var entropyHud = new Controls.PasswordEntropyHud 
        { 
            Margin = new Thickness(0, 0, 0, 14) 
        };

        passBox.PasswordChanged += (s, a) =>
        {
            var result = _vm.EvaluatePassword(passBox.Password);
            entropyHud.UpdatePassword(passBox.Password, result);
        };

        var kdfBox = new ComboBox 
        { 
            Header = "Key Derivation Function (KDF)", 
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 14) 
        };
        kdfBox.Items.Add("Argon2id (Memory-Hard, RFC 9106) [Recommended]");
        kdfBox.Items.Add("PBKDF2-HMAC-SHA256 (600,000 Rounds)");
        kdfBox.SelectedIndex = 0;

        var warningText = new TextBlock
        {
            Text = "IMPORTANT: PHANTOMVAULT implements zero-backdoor local cryptography. If you lose or forget your master password, your files cannot be recovered by any party.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["WarningBrush"],
            Margin = new Thickness(0, 0, 0, 12)
        };

        var stack = new StackPanel
        {
            Width = 460,
            Spacing = 2,
            Children = { nameBox, pathBox, browseButton, passBox, confirmBox, entropyHud, kdfBox, warningText }
        };

        var dialog = new ContentDialog
        {
            Title = "Create New Encrypted Vault",
            Content = stack,
            PrimaryButtonText = "Create Encrypted Vault",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            string vaultName = string.IsNullOrWhiteSpace(nameBox.Text) ? "NewVault" : nameBox.Text.Trim();
            if (string.IsNullOrEmpty(passBox.Password) || passBox.Password != confirmBox.Password)
            {
                _vm.ShowStatus("Passwords do not match or are empty. Vault creation cancelled.", InfoBarSeverity.Warning);
                return;
            }

            var kdf = kdfBox.SelectedIndex == 0 ? KdfAlgorithm.Argon2id : KdfAlgorithm.Pbkdf2HmacSha256;
            await _vm.CreateNewVaultAsync((vaultName, pathBox.Text, passBox.Password, kdf));
            UpdateUI();
        }
    }

    private async void UnlockVaultCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is VaultDescriptor descriptor)
        {
            _vm.SelectedVault = descriptor;

            // Prompt for password
            var passBox = new PasswordBox 
            { 
                PlaceholderText = "Enter master password for " + descriptor.Name,
                HorizontalAlignment = HorizontalAlignment.Stretch 
            };

            var dialog = new ContentDialog
            {
                Title = $"Unlock Vault: {descriptor.Name}",
                Content = new StackPanel
                {
                    Width = 360,
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock 
                        { 
                            Text = "Keys are securely derived into volatile memory only. They are zeroized immediately on lock.", 
                            FontSize = 12, 
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SecondaryTextBrush"]
                        },
                        passBox
                    }
                },
                PrimaryButtonText = "Unlock",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var res = await dialog.ShowAsync();
            if (res == ContentDialogResult.Primary && !string.IsNullOrEmpty(passBox.Password))
            {
                await _vm.UnlockCurrentVaultAsync(passBox.Password);
                if (_vaultRepo.IsVaultUnlocked(descriptor.Id))
                {
                    descriptor.State = VaultState.Unlocked;
                }
                UpdateUI();
            }
        }
    }

    private async void LockVaultCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is VaultDescriptor descriptor)
        {
            _vm.SelectedVault = descriptor;
            await _vm.LockCurrentVaultAsync();
            descriptor.State = VaultState.Locked;
            UpdateUI();
        }
    }

    private void OpenWorkspaceCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is VaultDescriptor descriptor)
        {
            _vm.SelectedVault = descriptor;
            _vm.RequestNavigate("Vault");
        }
    }

    private void BackupVaultCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is VaultDescriptor descriptor)
        {
            _vm.SelectedVault = descriptor;
            _vm.RequestNavigate("Backup");
        }
    }

    private async void VerifyVaultCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is VaultDescriptor descriptor)
        {
            _vm.SelectedVault = descriptor;
            if (_vm.IsUnlocked)
            {
                await _vm.VerifyAllVaultFilesIntegrityAsync();
            }
            else
            {
                _vm.ShowStatus($"Please unlock '{descriptor.Name}' first to authenticate its ciphertext chunks.", InfoBarSeverity.Informational);
            }
        }
    }
}
