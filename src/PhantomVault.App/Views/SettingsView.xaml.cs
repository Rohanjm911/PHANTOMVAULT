using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PhantomVault_App.Helpers;
using PhantomVault_App.ViewModels;

namespace PhantomVault_App.Views;

public sealed partial class SettingsView : Page
{
    private readonly MainViewModel _vm;

    public SettingsView()
    {
        _vm = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _vm;
        InitializeComponent();
        Loaded += SettingsView_Loaded;
    }

    private void SettingsView_Loaded(object sender, RoutedEventArgs e)
    {
        DefaultVaultDirBox.Text = PhantomVault.Storage.StoragePaths.DefaultVaultsDirectory;
        if (_vm?.SoundService != null && SoundEffectsToggleSwitch != null)
        {
            SoundEffectsToggleSwitch.IsOn = _vm.SoundService.IsSoundEnabled;
        }
    }

    private async void BrowseDefaultDir_Click(object sender, RoutedEventArgs e)
    {
        string? folder = await FilePickerHelper.PickSingleFolderAsync();
        if (!string.IsNullOrEmpty(folder))
        {
            DefaultVaultDirBox.Text = folder;
            _vm?.ShowStatus("Default vault root path updated for this session.", InfoBarSeverity.Informational);
        }
    }

    private void SoundEffectsToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_vm?.SoundService != null && SoundEffectsToggleSwitch != null)
        {
            _vm.SoundService.IsSoundEnabled = SoundEffectsToggleSwitch.IsOn;
        }
    }

    private void TestUnlockSound_Click(object sender, RoutedEventArgs e) => _vm.SoundService.PlayUnlockSound();
    private void TestLockSound_Click(object sender, RoutedEventArgs e) => _vm.SoundService.PlayLockSound();
    private void TestShredSound_Click(object sender, RoutedEventArgs e) => _vm.SoundService.PlayShredSound();
    private void TestStegoSound_Click(object sender, RoutedEventArgs e) => _vm.SoundService.PlayStegoSound();
    private void TestEmergencySound_Click(object sender, RoutedEventArgs e) => _vm.SoundService.PlayEmergencySound();
}
