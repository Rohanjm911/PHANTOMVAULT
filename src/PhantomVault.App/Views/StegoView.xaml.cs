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

                CarrierCapacityText.Text = FormatBytes(_vm.StegoCarrierInfo.UsableCapacityBytes);
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
        }
    }

    private void StegoPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _vm.StegoPassword = StegoPasswordBox.Password;
        var strength = _vm.EvaluatePassword(StegoPasswordBox.Password);
        StegoEntropyText.Text = $"Password Strength: {strength.Label} ({strength.EstimatedEntropyBits:F1} bits entropy)";
    }

    private async void EmbedButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(CarrierPathBox.Text) || string.IsNullOrEmpty(PayloadPathBox.Text) || string.IsNullOrEmpty(StegoPasswordBox.Password))
        {
            _vm.ShowStatus("Please specify carrier, payload, and concealment password.", InfoBarSeverity.Warning);
            return;
        }

        string ext = Path.GetExtension(CarrierPathBox.Text);
        string? outPath = await FilePickerHelper.PickSaveFileAsync(
            $"{Path.GetFileNameWithoutExtension(CarrierPathBox.Text)}_concealed{ext}", 
            ext, 
            "Concealed Carrier");

        if (!string.IsNullOrEmpty(outPath))
        {
            _vm.StegoOutputPath = outPath;
            await _vm.EmbedStegoAsync();
        }
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

        if (string.IsNullOrEmpty(_vm.StegoExtractionOutDir))
        {
            _vm.StegoExtractionOutDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads";
        }

        await _vm.ExtractStegoAsync();
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{(bytes / 1024.0):F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{(bytes / (1024.0 * 1024.0)):F2} MB";
        return $"{(bytes / (1024.0 * 1024.0 * 1024.0)):F2} GB";
    }
}
