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
        InitializeComponent();
        _vm = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _vm;
        Loaded += SettingsView_Loaded;
    }

    private void SettingsView_Loaded(object sender, RoutedEventArgs e)
    {
        DefaultVaultDirBox.Text = PhantomVault.Storage.StoragePaths.DefaultVaultsDirectory;
    }

    private async void BrowseDefaultDir_Click(object sender, RoutedEventArgs e)
    {
        string? folder = await FilePickerHelper.PickSingleFolderAsync();
        if (!string.IsNullOrEmpty(folder))
        {
            DefaultVaultDirBox.Text = folder;
            _vm.ShowStatus("Default vault root path updated for this session.", InfoBarSeverity.Informational);
        }
    }
}
