using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PhantomVault.Core.Enums;
using PhantomVault.Core.Models;
using PhantomVault_App.Helpers;
using PhantomVault_App.ViewModels;
using Windows.ApplicationModel.DataTransfer;

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
            BreadcrumbVaultNameText.Text = _vm.SelectedVault.Name;
            UpdateLockDisplay(_vm.IsUnlocked);
        }
        else
        {
            BreadcrumbVaultNameText.Text = "No Vault Selected";
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
            CiphertextSizeText.Text = MainViewModel.FormatBytes(_vm.SelectedVault.TotalCiphertextBytes);
            FilesListView.ItemsSource = _vm.Entries;

            bool isEmpty = _vm.Entries.Count == 0;
            EmptyVaultWorkspaceBorder.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
            FilesListBorder.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private void VaultsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VaultsComboBox.SelectedItem is VaultDescriptor selected)
        {
            _vm.SelectedVault = selected;
            BreadcrumbVaultNameText.Text = selected.Name;
            UpdateLockDisplay(_vm.IsUnlocked);
        }
    }

    private async void UnlockButton_Click(object sender, RoutedEventArgs e)
    {
        UnlockErrorText.Visibility = Visibility.Collapsed;
        string password = UnlockPasswordBox.Password;

        if (string.IsNullOrEmpty(password))
        {
            UnlockErrorText.Text = "Master password cannot be empty.";
            UnlockErrorText.Visibility = Visibility.Visible;
            return;
        }

        await _vm.UnlockCurrentVaultAsync(password);
        UnlockPasswordBox.Password = string.Empty;

        if (_vm.IsUnlocked)
        {
            UpdateLockDisplay(true);
        }
        else
        {
            UnlockErrorText.Text = "Authentication failed. Incorrect master password.";
            UnlockErrorText.Visibility = Visibility.Visible;
        }
    }

    private void UnlockPasswordBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            UnlockButton_Click(sender, e);
        }
    }

    private async void LockButton_Click(object sender, RoutedEventArgs e)
    {
        await _vm.LockCurrentVaultAsync();
        UpdateLockDisplay(false);
    }

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        string? pickedPath = await FilePickerHelper.PickSingleFileAsync();
        if (!string.IsNullOrEmpty(pickedPath))
        {
            await _vm.ImportFileAsync(pickedPath);
            UpdateLockDisplay(_vm.IsUnlocked);
        }
    }

    private async void ExportEntryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is VaultEntry entry)
        {
            string ext = Path.GetExtension(entry.Name);
            string? destination = await FilePickerHelper.PickSaveFileAsync(entry.Name, ext);
            if (!string.IsNullOrEmpty(destination))
            {
                await _vm.ExportFileAsync((entry.Id, destination));
            }
        }
    }

    private async void RenameEntryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is VaultEntry entry)
        {
            var nameBox = new TextBox { Text = entry.Name, Header = "New File Name", Margin = new Thickness(0, 0, 0, 8) };
            var dialog = new ContentDialog
            {
                Title = "Rename Stored File",
                Content = nameBox,
                PrimaryButtonText = "Rename",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var res = await dialog.ShowAsync();
            if (res == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(nameBox.Text) && nameBox.Text != entry.Name)
            {
                await _vm.RenameEntryAsync((entry.Id, nameBox.Text.Trim()));
                UpdateLockDisplay(_vm.IsUnlocked);
            }
        }
    }

    private async void DeleteEntryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is VaultEntry entry)
        {
            var dialog = new ContentDialog
            {
                Title = "Delete File from Encrypted Vault",
                Content = $"Are you sure you want to permanently delete '{entry.Name}' from this encrypted vault? Ciphertext on disk will be unlinked and purged.",
                PrimaryButtonText = "Delete Permanently",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var res = await dialog.ShowAsync();
            if (res == ContentDialogResult.Primary)
            {
                await _vm.DeleteFileAsync(entry.Id);
                UpdateLockDisplay(_vm.IsUnlocked);
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
        _ = _vm.LoadEntriesAsync().ContinueWith(_ =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateLockDisplay(_vm.IsUnlocked);
            });
        });
    }

    private void NewVaultButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.RequestNavigate("Vaults");
    }

    private void FilesArea_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Encrypt & Store in Vault";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsContentVisible = true;
            DropTargetOverlay.Visibility = Visibility.Visible;
        }
    }

    private void FilesArea_DragLeave(object sender, DragEventArgs e)
    {
        DropTargetOverlay.Visibility = Visibility.Collapsed;
    }

    private async void FilesArea_Drop(object sender, DragEventArgs e)
    {
        DropTargetOverlay.Visibility = Visibility.Collapsed;
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            int importedCount = 0;
            foreach (var item in items)
            {
                if (item is Windows.Storage.StorageFile file)
                {
                    await _vm.ImportFileAsync(file.Path);
                    importedCount++;
                }
            }

            if (importedCount > 0)
            {
                UpdateLockDisplay(_vm.IsUnlocked);
            }
        }
    }
}
