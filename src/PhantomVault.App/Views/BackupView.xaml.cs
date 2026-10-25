using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PhantomVault_App.Helpers;
using PhantomVault_App.ViewModels;

namespace PhantomVault_App.Views;

public sealed partial class BackupView : Page
{
    private readonly MainViewModel _vm;

    public BackupView()
    {
        _vm = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _vm;
        InitializeComponent();
    }

    private async void CreateBackupButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedVault == null)
        {
            _vm.ShowStatus("Please select a vault to back up.", InfoBarSeverity.Warning);
            return;
        }

        string defaultName = $"{_vm.SelectedVault.Name}_Backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pvbackup";
        string? destPath = await FilePickerHelper.PickSaveFileAsync(defaultName, ".pvbackup", "PHANTOMVAULT Backup Archive");

        if (!string.IsNullOrEmpty(destPath))
        {
            await _vm.CreateBackupAsync(destPath);
        }
    }

    private async void BrowseArchiveButton_Click(object sender, RoutedEventArgs e)
    {
        string? archivePath = await FilePickerHelper.PickSingleFileAsync(".pvbackup", ".zip");
        if (!string.IsNullOrEmpty(archivePath))
        {
            ArchiveFilePathBox.Text = archivePath;
            await _vm.ValidateBackupArchiveAsync(archivePath);

            if (_vm.InspectedManifest != null)
            {
                ManifestCard.Visibility = Visibility.Visible;
                ManifestVaultNameText.Text = _vm.InspectedManifest.VaultName;
                ManifestTimestampText.Text = _vm.InspectedManifest.CreatedAtUtc.ToString("f");
                ManifestFileCountText.Text = $"{_vm.InspectedManifest.FileCount} files ({FormatBytes(_vm.InspectedManifest.TotalCiphertextBytes)})";
                ManifestHeaderHashText.Text = _vm.InspectedManifest.HeaderSha256;
            }
        }
    }

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ArchiveFilePathBox.Text) || _vm.InspectedManifest == null)
        {
            _vm.ShowStatus("Please select a validated backup archive first.", InfoBarSeverity.Warning);
            return;
        }

        string? restoreFolder = await FilePickerHelper.PickSingleFolderAsync();
        if (!string.IsNullOrEmpty(restoreFolder))
        {
            string newName = $"{_vm.InspectedManifest.VaultName}_Restored";
            await _vm.RestoreBackupAsync((ArchiveFilePathBox.Text, restoreFolder, newName));
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
