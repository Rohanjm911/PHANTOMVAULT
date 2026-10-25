using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PhantomVault.Core.Models;
using PhantomVault_App.Helpers;
using PhantomVault_App.ViewModels;

namespace PhantomVault_App.Views;

public sealed partial class VaultView : Page
{
    private readonly MainViewModel _vm;

    public VaultView()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _vm;
        Loaded += VaultView_Loaded;
    }

    private async void VaultView_Loaded(object sender, RoutedEventArgs e)
    {
        await _vm.LoadVaultsAsync();
        RefreshVaultUi();
    }

    private void RefreshVaultUi()
    {
        VaultsComboBox.ItemsSource = _vm.Vaults;
        if (_vm.SelectedVault != null)
        {
            VaultsComboBox.SelectedItem = _vm.SelectedVault;
            UpdateLockDisplay(_vm.IsUnlocked);
        }
        else
        {
            UpdateLockDisplay(false);
        }
    }

    private void UpdateLockDisplay(bool isUnlocked)
    {
        LockedPanel.Visibility = isUnlocked ? Visibility.Collapsed : Visibility.Visible;
        UnlockedPanel.Visibility = isUnlocked ? Visibility.Visible : Visibility.Collapsed;

        if (isUnlocked && _vm.SelectedVault != null)
        {
            FileCountText.Text = _vm.SelectedVault.FileCount.ToString("N0");
            CiphertextSizeText.Text = FormatBytes(_vm.SelectedVault.TotalCiphertextBytes);
            FilesListView.ItemsSource = _vm.Entries;
        }
    }

    private void VaultsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VaultsComboBox.SelectedItem is VaultDescriptor vault)
        {
            _vm.SelectedVault = vault;
            UpdateLockDisplay(_vm.IsUnlocked);
        }
    }

    private async void UnlockButton_Click(object sender, RoutedEventArgs e)
    {
        await AttemptUnlockAsync();
    }

    private async void UnlockPasswordBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await AttemptUnlockAsync();
        }
    }

    private async Task AttemptUnlockAsync()
    {
        string password = UnlockPasswordBox.Password;
        if (string.IsNullOrEmpty(password))
        {
            UnlockErrorText.Text = "Please enter your master password.";
            UnlockErrorText.Visibility = Visibility.Visible;
            return;
        }

        UnlockErrorText.Visibility = Visibility.Collapsed;
        await _vm.UnlockCurrentVaultAsync(password);

        if (_vm.IsUnlocked)
        {
            UnlockPasswordBox.Password = string.Empty;
            UpdateLockDisplay(true);
        }
        else
        {
            UnlockErrorText.Text = "Incorrect password or corrupted vault header.";
            UnlockErrorText.Visibility = Visibility.Visible;
        }
    }

    private async void LockButton_Click(object sender, RoutedEventArgs e)
    {
        await _vm.LockCurrentVaultAsync();
        UpdateLockDisplay(false);
    }

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        string? pickedPath = await FilePickerHelper.PickSingleFileAsync("*");
        if (!string.IsNullOrEmpty(pickedPath))
        {
            await _vm.ImportFileAsync(pickedPath);
            UpdateLockDisplay(true);
        }
    }

    private async void ExportEntryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is VaultEntry entry)
        {
            string ext = Path.GetExtension(entry.Name);
            string? destPath = await FilePickerHelper.PickSaveFileAsync(entry.Name, string.IsNullOrEmpty(ext) ? ".*" : ext, "Exported File");
            if (!string.IsNullOrEmpty(destPath))
            {
                await _vm.ExportFileAsync((entry.Id, destPath));
            }
        }
    }

    private async void DeleteEntryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is VaultEntry entry)
        {
            var dialog = new ContentDialog
            {
                Title = "Permanently Delete File?",
                Content = $"Are you sure you want to permanently destroy and remove '{entry.Name}' from this encrypted vault?",
                PrimaryButtonText = "Delete Permanently",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await _vm.DeleteFileAsync(entry.Id);
                UpdateLockDisplay(true);
            }
        }
    }

    private async void VerifyAllButton_Click(object sender, RoutedEventArgs e)
    {
        await _vm.VerifyAllVaultFilesIntegrityAsync();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _vm.SearchQuery = SearchBox.Text;
        _ = _vm.LoadEntriesAsync();
    }

    private async void NewVaultButton_Click(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox { PlaceholderText = "Vault Name (e.g. PersonalVault)", Margin = new Thickness(0, 0, 0, 8) };
        var passBox = new PasswordBox { PlaceholderText = "Master Password (min 12 characters recommended)", Margin = new Thickness(0, 0, 0, 8) };
        var confirmBox = new PasswordBox { PlaceholderText = "Confirm Master Password", Margin = new Thickness(0, 0, 0, 8) };
        var strengthText = new TextBlock { Text = "Password Strength: Not Evaluated", FontSize = 12, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleHeaderBrush"] };

        passBox.PasswordChanged += (s, args) =>
        {
            var strength = _vm.EvaluatePassword(passBox.Password);
            strengthText.Text = $"Password Strength: {strength.Label} ({strength.EstimatedEntropyBits:F1} bits entropy)";
        };

        var stack = new StackPanel
        {
            Spacing = 6,
            Children = { nameBox, passBox, confirmBox, strengthText }
        };

        var dialog = new ContentDialog
        {
            Title = "Create New Encrypted Vault",
            Content = stack,
            PrimaryButtonText = "Create Vault",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var res = await dialog.ShowAsync();
        if (res == ContentDialogResult.Primary)
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text))
            {
                _vm.ShowStatus("Vault name cannot be empty.", InfoBarSeverity.Error);
                return;
            }

            if (string.IsNullOrEmpty(passBox.Password) || passBox.Password != confirmBox.Password)
            {
                _vm.ShowStatus("Passwords do not match or are empty.", InfoBarSeverity.Error);
                return;
            }

            string baseDir = PhantomVault.Storage.StoragePaths.DefaultVaultsDirectory;
            await _vm.CreateNewVaultAsync((nameBox.Text, baseDir, passBox.Password, PhantomVault.Core.Enums.KdfAlgorithm.Argon2id));
            RefreshVaultUi();
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{(bytes / 1024.0):F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{(bytes / (1024.0 * 1024.0)):F2} MB";
        return $"{(bytes / (1024.0 * 1024.0 * 1024.0)):F2} GB";
    }
}
