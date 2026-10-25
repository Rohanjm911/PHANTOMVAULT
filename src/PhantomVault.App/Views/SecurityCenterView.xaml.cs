using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PhantomVault_App.ViewModels;

namespace PhantomVault_App.Views;

public sealed partial class SecurityCenterView : Page
{
    private readonly MainViewModel _vm;

    public SecurityCenterView()
    {
        _vm = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _vm;
        InitializeComponent();
        Loaded += (s, e) =>
        {
            if (_vm == null) return;
            LastIntegrityCheckText.Text = $"Status: {_vm.LastIntegrityCheckResult}";
            LastBackupStatusText.Text = _vm.LastBackupDateText;
        };
    }

    private async void VerifyButton_Click(object sender, RoutedEventArgs e)
    {
        await _vm.VerifyAllVaultFilesIntegrityAsync();
        LastIntegrityCheckText.Text = $"Status: {_vm.LastIntegrityCheckResult}";
    }

    private async void EmergencyLockButton_Click(object sender, RoutedEventArgs e)
    {
        await _vm.LockAllVaultsAsync();
    }
}
