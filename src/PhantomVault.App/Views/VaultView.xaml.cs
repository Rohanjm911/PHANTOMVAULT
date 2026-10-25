using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using PhantomVault.Core.Enums;
using PhantomVault.Core.Models;
using PhantomVault_App.Helpers;
using PhantomVault_App.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace PhantomVault_App.Views;

public sealed partial class VaultView : Page
{
    private readonly MainViewModel _vm;
    private bool _isGridView = false;

    public VaultView()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _vm;
        Loaded += VaultView_Loaded;
    }

    private void VaultView_Loaded(object sender, RoutedEventArgs e)
    {
        VaultsComboBox.ItemsSource = _vm.Vaults;
        if (_vm.SelectedVault != null)
        {
            VaultsComboBox.SelectedItem = _vm.SelectedVault;
            BreadcrumbVaultNameText.Text = _vm.SelectedVault.Name;
        }

        UpdateLockDisplay(_vm.IsUnlocked);
        WindowsHelloUnlockButton.Visibility = _vm.IsWindowsHelloAvailable ? Visibility.Visible : Visibility.Collapsed;
    }

    private void VaultsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VaultsComboBox.SelectedItem is VaultDescriptor vault)
        {
            _vm.SelectedVault = vault;
            BreadcrumbVaultNameText.Text = vault.Name;
            UpdateLockDisplay(_vm.IsUnlocked);
        }
    }

    private void UpdateLockDisplay(bool isUnlocked)
    {
        LockedPanel.Visibility = isUnlocked ? Visibility.Collapsed : Visibility.Visible;
        UnlockedPanel.Visibility = isUnlocked ? Visibility.Visible : Visibility.Collapsed;

        if (isUnlocked && _vm.SelectedVault != null)
        {
            FileCountText.Text = _vm.Entries.Count.ToString();
            CiphertextSizeText.Text = MainViewModel.FormatBytes(_vm.SelectedVault.TotalCiphertextBytes);
            FilesListView.ItemsSource = _vm.Entries;
            FilesGridView.ItemsSource = _vm.Entries;

            bool hasFiles = _vm.Entries.Count > 0;
            EmptyVaultWorkspaceBorder.Visibility = hasFiles ? Visibility.Collapsed : Visibility.Visible;
            FilesListBorder.Visibility = hasFiles && !_isGridView ? Visibility.Visible : Visibility.Collapsed;
            FilesGridBorder.Visibility = hasFiles && _isGridView ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private async void UnlockButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedVault == null) return;
        string password = UnlockPasswordBox.Password;
        if (string.IsNullOrEmpty(password))
        {
            UnlockErrorText.Text = "Please enter your master password.";
            UnlockErrorText.Visibility = Visibility.Visible;
            return;
        }

        UnlockErrorText.Visibility = Visibility.Collapsed;
        try
        {
            await _vm.UnlockCurrentVaultAsync(password);
            UnlockPasswordBox.Password = string.Empty;
            UpdateLockDisplay(_vm.IsUnlocked);
        }
        catch (Exception ex)
        {
            UnlockErrorText.Text = $"Unlock failed: {ex.Message}";
            UnlockErrorText.Visibility = Visibility.Visible;
        }
    }

    private async void WindowsHelloUnlockButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedVault == null) return;

        bool verified = await _vm.VerifyWindowsHelloConsentAsync($"Confirm your identity to unseal vault '{_vm.SelectedVault.Name}'");
        if (verified)
        {
            _vm.ShowStatus("Windows Hello biometric confirmed. Please enter master password to derive cryptographic key.", InfoBarSeverity.Informational);
            UnlockPasswordBox.Focus(FocusState.Programmatic);
        }
        else
        {
            _vm.ShowStatus("Windows Hello verification cancelled or failed.", InfoBarSeverity.Warning);
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
        await ImportFileInternalAsync(false);
    }

    private async void ImportStandard_Click(object sender, RoutedEventArgs e)
    {
        await ImportFileInternalAsync(false);
    }

    private async void ImportAndShred_Click(object sender, RoutedEventArgs e)
    {
        var confirmDialog = new ContentDialog
        {
            Title = "DoD 5220.22-M Secure Shredding Warning",
            Content = "The original plaintext file on disk will be overwritten with 3 passes (zeros, ones, cryptographic random bytes) and permanently unlinked after encryption.\n\nThis operation cannot be undone. Do you wish to continue?",
            PrimaryButtonText = "Encrypt & Shred Original",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await confirmDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ImportFileInternalAsync(true);
        }
    }

    private async Task ImportFileInternalAsync(bool shredOriginal)
    {
        string? pickedPath = await FilePickerHelper.PickSingleFileAsync();
        if (!string.IsNullOrEmpty(pickedPath))
        {
            await _vm.ImportFileWithShredOptionAsync(pickedPath, shredOriginal);
            UpdateLockDisplay(_vm.IsUnlocked);
        }
    }

    private async void StandaloneShredderButton_Click(object sender, RoutedEventArgs e)
    {
        string? pickedPath = await FilePickerHelper.PickSingleFileAsync();
        if (string.IsNullOrEmpty(pickedPath)) return;

        var fi = new FileInfo(pickedPath);
        var confirmDialog = new ContentDialog
        {
            Title = "Confirm DoD 5220.22-M File Shredding",
            Content = $"Are you sure you want to permanently shred and destroy:\n\n{fi.FullName}\nSize: {MainViewModel.FormatBytes(fi.Length)}\n\nPhysical disk sectors will be overwritten 3 times and filesystem metadata will be sanitized.",
            PrimaryButtonText = "Shred File Permanently",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await confirmDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await _vm.ShredExternalFileAsync(pickedPath);
        }
    }

    #region Zero-Disk-Leak In-App Previewer
    private static VaultEntry? GetEntryFromSender(object sender)
    {
        if (sender is FrameworkElement fe)
        {
            return fe.Tag as VaultEntry ?? fe.DataContext as VaultEntry;
        }
        return null;
    }

    private async void PreviewEntryButton_Click(object sender, RoutedEventArgs e)
    {
        var entry = GetEntryFromSender(sender);
        if (entry != null)
        {
            byte[]? data = await _vm.GetDecryptedBytesAsync(entry.Id);
            if (data == null || data.Length == 0) return;

            string ext = Path.GetExtension(entry.Name).ToLowerInvariant();
            var dialog = new ContentDialog
            {
                Title = $"Zero-Disk Preview: {entry.Name}",
                CloseButtonText = "Close & Zeroize RAM",
                XamlRoot = this.XamlRoot
            };

            try
            {
                if (IsImageExtension(ext))
                {
                    using var stream = new InMemoryRandomAccessStream();
                    using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                    {
                        writer.WriteBytes(data);
                        await writer.StoreAsync();
                    }

                    var bitmap = new BitmapImage();
                    await bitmap.SetSourceAsync(stream);

                    dialog.Content = new Grid
                    {
                        MaxHeight = 520,
                        MaxWidth = 700,
                        Children =
                        {
                            new Image { Source = bitmap, Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform }
                        }
                    };
                }
                else if (IsTextExtension(ext))
                {
                    string textContent = Encoding.UTF8.GetString(data);
                    dialog.Content = new ScrollViewer
                    {
                        MaxHeight = 480,
                        MaxWidth = 720,
                        Content = new TextBox
                        {
                            Text = textContent,
                            IsReadOnly = true,
                            AcceptsReturn = true,
                            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                            FontSize = 12,
                            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SecondaryBackgroundBrush"],
                            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["BorderBrush"],
                            Padding = new Thickness(14)
                        }
                    };
                }
                else
                {
                    // Hexadecimal Forensic Inspector
                    string hexDump = FormatHexDump(data);
                    dialog.Content = new ScrollViewer
                    {
                        MaxHeight = 480,
                        MaxWidth = 740,
                        Content = new TextBlock
                        {
                            Text = hexDump,
                            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                            FontSize = 11,
                            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["PrimaryAccentBrush"],
                            TextWrapping = TextWrapping.NoWrap
                        }
                    };
                }

                await dialog.ShowAsync();
            }
            finally
            {
                // CRITICAL: Scrub plaintext bytes from RAM buffer immediately upon closing
                CryptographicOperations.ZeroMemory(data);
            }
        }
    }

    private static bool IsImageExtension(string ext) =>
        ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp" or ".gif" or ".ico";

    private static bool IsTextExtension(string ext) =>
        ext is ".txt" or ".json" or ".csv" or ".md" or ".log" or ".xml" or ".cs" or ".sql" or ".html" or ".css" or ".yaml" or ".yml" or ".ini" or ".bat" or ".cmd" or ".ps1";

    private static string FormatHexDump(byte[] data, int maxBytes = 1024)
    {
        var sb = new StringBuilder();
        int length = Math.Min(data.Length, maxBytes);
        for (int i = 0; i < length; i += 16)
        {
            sb.Append($"{i:X8}  ");
            for (int j = 0; j < 16; j++)
            {
                if (i + j < length)
                    sb.Append($"{data[i + j]:X2} ");
                else
                    sb.Append("   ");
                if (j == 7) sb.Append(" ");
            }
            sb.Append(" |");
            for (int j = 0; j < 16 && (i + j) < length; j++)
            {
                byte b = data[i + j];
                sb.Append(b >= 32 && b <= 126 ? (char)b : '.');
            }
            sb.AppendLine("|");
        }
        if (data.Length > maxBytes)
        {
            sb.AppendLine($"\n... [{data.Length - maxBytes:N0} additional bytes omitted from preview] ...");
        }
        return sb.ToString();
    }
    #endregion

    private async void ExportEntryButton_Click(object sender, RoutedEventArgs e)
    {
        var entry = GetEntryFromSender(sender);
        if (entry != null)
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
        var entry = GetEntryFromSender(sender);
        if (entry != null)
        {
            var box = new TextBox { Text = entry.Name, Header = "Enter new name" };
            var dialog = new ContentDialog
            {
                Title = "Rename File",
                Content = box,
                PrimaryButtonText = "Rename",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var res = await dialog.ShowAsync();
            if (res == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(box.Text))
            {
                await _vm.RenameEntryAsync((entry.Id, box.Text.Trim()));
                UpdateLockDisplay(_vm.IsUnlocked);
            }
        }
    }

    private async void DeleteEntryButton_Click(object sender, RoutedEventArgs e)
    {
        var entry = GetEntryFromSender(sender);
        if (entry != null)
        {
            var dialog = new ContentDialog
            {
                Title = "Confirm File Deletion",
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

    private void ViewListButton_Click(object sender, RoutedEventArgs e)
    {
        _isGridView = false;
        ViewListButton.Style = (Style)Application.Current.Resources["PrimaryButtonStyle"];
        ViewGridButton.Style = (Style)Application.Current.Resources["SecondaryButtonStyle"];
        UpdateLockDisplay(_vm.IsUnlocked);
    }

    private void ViewGridButton_Click(object sender, RoutedEventArgs e)
    {
        _isGridView = true;
        ViewListButton.Style = (Style)Application.Current.Resources["SecondaryButtonStyle"];
        ViewGridButton.Style = (Style)Application.Current.Resources["PrimaryButtonStyle"];
        UpdateLockDisplay(_vm.IsUnlocked);
    }

    private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SortComboBox.SelectedItem is ComboBoxItem item && item.Tag is string sortTag)
        {
            _vm.SortEntries(sortTag);
            UpdateLockDisplay(_vm.IsUnlocked);
        }
    }

    #region Drag & Drop Handlers
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
    #endregion

    #region Plausible Deniability Handlers
    private async void SetupDuressButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedVault == null) return;

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = "Configure a secret secondary password to disclose if forced to unlock under physical duress or coercion. Entering this password will seamlessly unseal an isolated Decoy Vault populated with realistic decoy files, keeping your authentic vault cryptographically untouched.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SecondaryTextBrush"],
            FontSize = 12
        });

        var duressBox = new PasswordBox { Header = "Duress / Coercion Password", PlaceholderText = "Enter decoy unsealing password" };
        var confirmBox = new PasswordBox { Header = "Confirm Duress Password", PlaceholderText = "Re-enter duress password" };
        panel.Children.Add(duressBox);
        panel.Children.Add(confirmBox);

        var dialog = new ContentDialog
        {
            Title = "Plausible Deniability Setup",
            Content = panel,
            PrimaryButtonText = "Activate Decoy Enclave",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var res = await dialog.ShowAsync();
        if (res == ContentDialogResult.Primary)
        {
            if (string.IsNullOrWhiteSpace(duressBox.Password) || duressBox.Password.Length < 6)
            {
                _vm.ShowStatus("Duress password must be at least 6 characters.", InfoBarSeverity.Error);
                return;
            }
            if (duressBox.Password != confirmBox.Password)
            {
                _vm.ShowStatus("Duress passwords do not match.", InfoBarSeverity.Error);
                return;
            }

            await _vm.SetupDuressPasswordAsync(duressBox.Password);
        }
    }
    #endregion
}
