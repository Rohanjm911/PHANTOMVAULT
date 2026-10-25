using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PhantomVault.Core.Enums;
using PhantomVault_App.Helpers;
using PhantomVault_App.ViewModels;

namespace PhantomVault_App.Views;

public sealed partial class StegoView : Page
{
    private readonly MainViewModel _vm;

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
            CarrierPathBox.Text = path;
            await _vm.AnalyzeCarrierAsync(path);

            if (_vm.StegoCarrierInfo != null)
            {
                CapacityReportGrid.Visibility = Visibility.Visible;
                CarrierTypeText.Text = _vm.StegoCarrierInfo.CarrierType == CarrierType.PngImage ? "PNG Image" : "16-bit PCM WAV Audio";
                CarrierDimsText.Text = _vm.StegoCarrierInfo.CarrierType == CarrierType.PngImage 
                    ? $"{_vm.StegoCarrierInfo.Width} x {_vm.StegoCarrierInfo.Height} ({_vm.StegoCarrierInfo.Channels} ch)" 
                    : $"{_vm.StegoCarrierInfo.TotalCarrierSizeBytes / 2:N0} samples ({_vm.StegoCarrierInfo.Channels} ch)";

                CarrierCapacityText.Text = MainViewModel.FormatBytes(_vm.StegoCarrierInfo.UsableCapacityBytes);

                // Auto-suggest default output path
                string ext = Path.GetExtension(path);
                string outPath = Path.Combine(Path.GetDirectoryName(path)!, $"{Path.GetFileNameWithoutExtension(path)}_concealed{ext}");
                OutputPathBox.Text = outPath;
                _vm.StegoOutputPath = outPath;

                UpdateCapacityUi();
            }
        }
    }

    private async void BrowsePayloadButton_Click(object sender, RoutedEventArgs e)
    {
        string? path = await FilePickerHelper.PickSingleFileAsync("*");
        if (!string.IsNullOrEmpty(path))
        {
            PayloadPathBox.Text = path;
            _vm.StegoPayloadPath = path;

            var fi = new FileInfo(path);
            PayloadDetailsText.Text = $"File: {fi.Name} • Size: {MainViewModel.FormatBytes(fi.Length)} ({fi.Length:N0} bytes)";
            PayloadDetailsText.Visibility = Visibility.Visible;

            UpdateCapacityUi();
        }
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
        StegoEntropyText.Text = $"Password Strength: {strength.Label} (~{strength.EstimatedEntropyBits:F0} bits entropy)";
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
    }

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
            _vm.ShowStatus("Concealed carrier and decryption password are required.", InfoBarSeverity.Warning);
            return;
        }

        await _vm.ExtractStegoAsync();
    }
}
