using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using PhantomVault.Core.Interfaces;
using PhantomVault.Core.Models;
using PhantomVault_App.ViewModels;

namespace PhantomVault_App.Views;

public sealed partial class SettingsView : Page
{
    private readonly MainViewModel _vm;
    private readonly ISecurityPolicyService _policyService;

    public SettingsView()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<MainViewModel>();
        _policyService = App.Services.GetRequiredService<ISecurityPolicyService>();
        DataContext = _vm;

        AutoLockToggle.IsOn = _policyService.CurrentPolicy.AutoLockOnInactivity;
        TimeoutSlider.Value = _policyService.CurrentPolicy.InactivityMinutes;
        SessionLockToggle.IsOn = _policyService.CurrentPolicy.LockOnWorkstationLock;
        DeadMansToggle.IsOn = _policyService.CurrentPolicy.DeadMansSwitchEnabled;
    }

    private void TimeoutSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (TimeoutLabel != null)
        {
            TimeoutLabel.Text = $"Inactivity Timeout: {(int)e.NewValue} minutes";
        }
        PolicyChanged(sender, null);
    }

    private void PolicyChanged(object sender, RoutedEventArgs? e)
    {
        if (_policyService == null) return;

        var policy = new EmergencyPolicy
        {
            AutoLockOnInactivity = AutoLockToggle?.IsOn ?? true,
            InactivityMinutes = (int)(TimeoutSlider?.Value ?? 15),
            LockOnWorkstationLock = SessionLockToggle?.IsOn ?? true,
            DeadMansSwitchEnabled = DeadMansToggle?.IsOn ?? false,
            DeadMansSwitchTimeoutMinutes = (int)(TimeoutSlider?.Value ?? 15) * 2
        };

        _policyService.UpdatePolicy(policy);
    }
}
