using System;
using System.ComponentModel;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PhantomVault_App.ViewModels;

namespace PhantomVault_App.Views;

public sealed partial class DashboardView : Page
{
    private readonly MainViewModel _vm;

    public DashboardView()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<MainViewModel>();
        _vm.PropertyChanged += Vm_PropertyChanged;
        Loaded += DashboardView_Loaded;
    }

    private void DashboardView_Loaded(object sender, RoutedEventArgs e)
    {
        _vm.RefreshDashboardStats();
        UpdateUI();
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(UpdateUI);
    }

    private void UpdateUI()
    {
        TotalVaultsText.Text = _vm.TotalVaultsCount.ToString();
        LockedVaultsText.Text = _vm.LockedVaultsCount.ToString();
        UnlockedVaultsText.Text = _vm.UnlockedVaultsCount.ToString();
        EncryptedStorageText.Text = _vm.TotalEncryptedStorageText;
        LastBackupText.Text = _vm.LastBackupDateText;
        LastIntegrityCheckDashboardText.Text = _vm.LastIntegrityCheckResult;

        NoticesItemsControl.ItemsSource = _vm.SecurityNotices;
        RecentActivityListView.ItemsSource = _vm.ActivityLogs.Take(4).ToList();
    }

    private void QuickCreateVault_Click(object sender, RoutedEventArgs e)
    {
        _vm.RequestNavigate("Vaults");
    }

    private void QuickUnlockVault_Click(object sender, RoutedEventArgs e)
    {
        _vm.RequestNavigate("Vault");
    }

    private void QuickImportFiles_Click(object sender, RoutedEventArgs e)
    {
        _vm.RequestNavigate("Vault");
    }

    private void QuickHideData_Click(object sender, RoutedEventArgs e)
    {
        _vm.RequestNavigate("Stego");
    }

    private void QuickExtractData_Click(object sender, RoutedEventArgs e)
    {
        _vm.RequestNavigate("Stego");
    }

    private void QuickCreateBackup_Click(object sender, RoutedEventArgs e)
    {
        _vm.RequestNavigate("Backup");
    }

    private async void VerifyIntegrity_Click(object sender, RoutedEventArgs e)
    {
        await _vm.VerifyAllVaultFilesIntegrityAsync();
    }
}
