using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using PhantomVault.Core.Enums;
using PhantomVault_App.Helpers;
using PhantomVault_App.ViewModels;
using Windows.ApplicationModel.DataTransfer;

namespace PhantomVault_App.Views;

public sealed partial class StegoView : Page
{
    private readonly MainViewModel _vm;
    private string _lastGeneratedStegoPath = string.Empty;

    public StegoView()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _vm;
    }

    private async void BrowseCarrierButton_Click(object sender, RoutedEventArgs e)
    {
        string? path = await FilePickerHelper.PickSingleFileAsync(".png", ".wav");
        if (!string.IsNullOrEmpty(path))
        {
            await ProcessCarrierSelectionAsync(path);
        }
    }

    private async void BrowsePayloadButton_Click(object sender, RoutedEventArgs e)
    {
        string? path = await FilePickerHelper.PickSingleFileAsync("*");
        if (!string.IsNullOrEmpty(path))
        {
            ProcessPayloadSelection(path);
        }
    }

    private async System.Threading.Tasks.Task ProcessCarrierSelectionAsync(string path)
    {
        CarrierPathBox.Text = path;
        await _vm.AnalyzeCarrierAsync(path);

        if (_vm.StegoCarrierInfo != null)
        {
            CapacityReportGrid.Visibility = Visibility.Visible;
            bool isPng = _vm.StegoCarrierInfo.CarrierType == CarrierType.PngImage;

            CarrierTypeText.Text = isPng ? "PNG Image" : "16-bit PCM WAV Audio";
            CarrierFormatText.Text = isPng ? $"RGBA ({_vm.StegoCarrierInfo.Channels} channels)" : $"PCM Stereo ({_vm.StegoCarrierInfo.Channels} ch)";
            CarrierDimsText.Text = isPng 
                ? $"{_vm.StegoCarrierInfo.Width} x {_vm.StegoCarrierInfo.Height}" 
                : $"{_vm.StegoCarrierInfo.TotalCarrierSizeBytes / 2:N0} samples";

            var fi = new FileInfo(path);
            CarrierFileSizeText.Text = $"{MainViewModel.FormatBytes(fi.Length)} on disk";
            CarrierCapacityText.Text = MainViewModel.FormatBytes(_vm.StegoCarrierInfo.UsableCapacityBytes);

            // Thumbnail / Icon & Audio Auditor Panel
            if (isPng && File.Exists(path))
            {
                CarrierThumbnailImage.Source = new BitmapImage(new Uri(path));
                CarrierThumbnailImage.Visibility = Visibility.Visible;
                CarrierAudioIcon.Visibility = Visibility.Collapsed;
                AudioCarrierAuditorBorder.Visibility = Visibility.Collapsed;
            }
            else
            {
                CarrierThumbnailImage.Visibility = Visibility.Collapsed;
                CarrierAudioIcon.Visibility = Visibility.Visible;
                AudioCarrierAuditorBorder.Visibility = Visibility.Visible;
                AudioChannelsText.Text = _vm.StegoCarrierInfo.Channels == 1 ? "Mono (16-bit PCM)" : "Stereo (16-bit PCM)";
            }

            // Auto-suggest default output path
            string ext = Path.GetExtension(path);
            string outPath = Path.Combine(Path.GetDirectoryName(path)!, $"{Path.GetFileNameWithoutExtension(path)}_concealed{ext}");
            OutputPathBox.Text = outPath;
            _vm.StegoOutputPath = outPath;

            UpdateCapacityUi();
        }
    }

    private void ProcessPayloadSelection(string path)
    {
        PayloadPathBox.Text = path;
        _vm.StegoPayloadPath = path;

        var fi = new FileInfo(path);
        PayloadDetailsText.Text = $"File: {fi.Name} • Size: {MainViewModel.FormatBytes(fi.Length)} ({fi.Length:N0} bytes)";
        PayloadDetailsBorder.Visibility = Visibility.Visible;

        UpdateCapacityUi();
    }

    private void UpdateCapacityUi()
    {
        _vm.UpdateStegoCapacityStatus();
        CapacityCheckMessageText.Text = _vm.CapacityStatusMessage;
        CapacityCheckMessageText.Foreground = _vm.DoesPayloadFit
            ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SuccessBrush"]
            : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ErrorBrush"];
    }

    private void StegoPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _vm.StegoPassword = StegoPasswordBox.Password;
        var strength = _vm.EvaluatePassword(StegoPasswordBox.Password);
        StegoEntropyHud.UpdatePassword(StegoPasswordBox.Password, strength);
    }

    private async void BrowseSaveAs_Click(object sender, RoutedEventArgs e)
    {
        string ext = !string.IsNullOrEmpty(_vm.StegoCarrierPath) ? Path.GetExtension(_vm.StegoCarrierPath) : ".png";
        string defaultName = !string.IsNullOrEmpty(_vm.StegoCarrierPath) 
            ? $"{Path.GetFileNameWithoutExtension(_vm.StegoCarrierPath)}_concealed" 
            : "concealed_payload";

        string? outPath = await FilePickerHelper.PickSaveFileAsync(defaultName, ext);
        if (!string.IsNullOrEmpty(outPath))
        {
            OutputPathBox.Text = outPath;
            _vm.StegoOutputPath = outPath;
        }
    }

    private async void EmbedButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.StegoOutputPath = OutputPathBox.Text;

        if (string.IsNullOrEmpty(_vm.StegoCarrierPath) || string.IsNullOrEmpty(_vm.StegoPayloadPath) || string.IsNullOrEmpty(_vm.StegoPassword))
        {
            _vm.ShowStatus("Please specify carrier, payload, and concealment password.", InfoBarSeverity.Warning);
            return;
        }

        if (!_vm.DoesPayloadFit)
        {
            _vm.ShowStatus("Cannot embed: secret payload exceeds available carrier capacity.", InfoBarSeverity.Error);
            return;
        }

        await _vm.EmbedStegoAsync();

        string outPath = _vm.StegoOutputPath;
        if (File.Exists(outPath))
        {
            _lastGeneratedStegoPath = outPath;
            ShowComparisonResults(outPath);
        }
    }

    private void ShowComparisonResults(string stegoPath)
    {
        StegoResultCard.Visibility = Visibility.Visible;
        bool isPng = _vm.StegoCarrierInfo?.CarrierType == CarrierType.PngImage;

        if (isPng && File.Exists(_vm.StegoCarrierPath))
        {
            CompareOriginalImage.Source = new BitmapImage(new Uri(_vm.StegoCarrierPath));
            CompareOriginalImage.Visibility = Visibility.Visible;
            CompareOriginalAudioIcon.Visibility = Visibility.Collapsed;

            CompareStegoImage.Source = new BitmapImage(new Uri(stegoPath));
            CompareStegoImage.Visibility = Visibility.Visible;
            CompareStegoAudioIcon.Visibility = Visibility.Collapsed;
        }
        else
        {
            CompareOriginalImage.Visibility = Visibility.Collapsed;
            CompareOriginalAudioIcon.Visibility = Visibility.Visible;

            CompareStegoImage.Visibility = Visibility.Collapsed;
            CompareStegoAudioIcon.Visibility = Visibility.Visible;
        }

        var origFi = new FileInfo(_vm.StegoCarrierPath);
        var stegoFi = new FileInfo(stegoPath);
        CompareOriginalSpecsText.Text = $"{origFi.Name} ({MainViewModel.FormatBytes(origFi.Length)}) • Original";
        CompareStegoSpecsText.Text = $"{stegoFi.Name} ({MainViewModel.FormatBytes(stegoFi.Length)}) • Concealed";
        PlayStegoAudioButton.Visibility = isPng ? Visibility.Collapsed : Visibility.Visible;
    }

    #region Audio Carrier Playback & Acoustic Auditing
    private void PlayCarrierAudioButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_vm.StegoCarrierPath) || !File.Exists(_vm.StegoCarrierPath)) return;
        try
        {
            NativeAudioPlayer.PlayFile(_vm.StegoCarrierPath);
            _vm.ShowStatus("Playing pristine carrier audio...", InfoBarSeverity.Informational);
        }
        catch (Exception ex)
        {
            _vm.ShowStatus($"Audio playback error: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void StopCarrierAudioButton_Click(object sender, RoutedEventArgs e)
    {
        NativeAudioPlayer.Stop();
        _vm.ShowStatus("Audio playback stopped.", InfoBarSeverity.Informational);
    }

    private void PlayStegoAudioButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_lastGeneratedStegoPath) || !File.Exists(_lastGeneratedStegoPath)) return;
        try
        {
            NativeAudioPlayer.PlayFile(_lastGeneratedStegoPath);
            _vm.ShowStatus("Playing concealed stego audio (auditing zero acoustic distortion)...", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            _vm.ShowStatus($"Stego audio playback error: {ex.Message}", InfoBarSeverity.Error);
        }
    }
    #endregion

    private void OpenConcealedFile_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastGeneratedStegoPath) && File.Exists(_lastGeneratedStegoPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _lastGeneratedStegoPath,
                UseShellExecute = true
            });
        }
    }

    private void ShowInFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastGeneratedStegoPath) && File.Exists(_lastGeneratedStegoPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{_lastGeneratedStegoPath}\"",
                UseShellExecute = true
            });
        }
    }

    private void SwitchToExtract_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastGeneratedStegoPath))
        {
            ExtractCarrierPathBox.Text = _lastGeneratedStegoPath;
            _vm.StegoExtractionCarrierPath = _lastGeneratedStegoPath;
            StegoTabView.SelectedIndex = 1;
        }
    }

    #region Drag & Drop Event Handlers
    private void CarrierDrop_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Set as Carrier Media";
            e.DragUIOverride.IsCaptionVisible = true;
        }
    }

    private void CarrierDrop_DragLeave(object sender, DragEventArgs e)
    {
    }

    private async void CarrierDrop_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is Windows.Storage.StorageFile file)
            {
                string ext = Path.GetExtension(file.Path).ToLowerInvariant();
                if (ext == ".png" || ext == ".wav")
                {
                    await ProcessCarrierSelectionAsync(file.Path);
                }
                else
                {
                    _vm.ShowStatus("Invalid carrier format. Please select a .png image or .wav audio file.", InfoBarSeverity.Warning);
                }
            }
        }
    }

    private void PayloadDrop_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Set as Secret Payload";
            e.DragUIOverride.IsCaptionVisible = true;
        }
    }

    private void PayloadDrop_DragLeave(object sender, DragEventArgs e)
    {
    }

    private async void PayloadDrop_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is Windows.Storage.StorageFile file)
            {
                ProcessPayloadSelection(file.Path);
            }
        }
    }

    private async void ExtractCarrierDrop_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is Windows.Storage.StorageFile file)
            {
                ExtractCarrierPathBox.Text = file.Path;
                _vm.StegoExtractionCarrierPath = file.Path;
            }
        }
    }
    #endregion

    private async void BrowseExtractCarrier_Click(object sender, RoutedEventArgs e)
    {
        string? path = await FilePickerHelper.PickSingleFileAsync(".png", ".wav");
        if (!string.IsNullOrEmpty(path))
        {
            ExtractCarrierPathBox.Text = path;
            _vm.StegoExtractionCarrierPath = path;
        }
    }

    private async void BrowseExtractOutDir_Click(object sender, RoutedEventArgs e)
    {
        string? folder = await FilePickerHelper.PickSingleFolderAsync();
        if (!string.IsNullOrEmpty(folder))
        {
            ExtractOutDirBox.Text = folder;
            _vm.StegoExtractionOutDir = folder;
        }
    }

    private async void ExtractButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.StegoExtractionPassword = ExtractPasswordBox.Password;
        if (string.IsNullOrEmpty(_vm.StegoExtractionCarrierPath) || string.IsNullOrEmpty(_vm.StegoExtractionPassword))
        {
            _vm.ShowStatus("Carrier and extraction password are required.", InfoBarSeverity.Warning);
            return;
        }

        if (string.IsNullOrEmpty(_vm.StegoExtractionOutDir))
        {
            _vm.StegoExtractionOutDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            ExtractOutDirBox.Text = _vm.StegoExtractionOutDir;
        }

        await _vm.ExtractStegoAsync();
    }
}
