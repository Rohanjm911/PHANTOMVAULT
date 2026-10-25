using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PhantomVault_App.ViewModels;

namespace PhantomVault_App.Views;

public sealed partial class EmergencyAccessView : Page
{
    private readonly MainViewModel _vm;

    public EmergencyAccessView()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _vm;
        Loaded += EmergencyAccessView_Loaded;
    }

    private void EmergencyAccessView_Loaded(object sender, RoutedEventArgs e)
    {
        AutoLockToggle.IsOn = _vm.IsAutoLockEnabled;
        InactivitySlider.Value = _vm.InactivityTimeoutMinutes;
        InactivityTimeoutLabel.Text = $"{_vm.InactivityTimeoutMinutes} Minutes";

        SessionLockToggle.IsOn = _vm.IsSessionLockEnabled;

        DeadMansToggle.IsOn = _vm.IsDeadMansEnabled;
        DeadMansSlider.Value = _vm.DeadMansTimeoutMinutes;
        DeadMansTimeoutLabel.Text = $"{_vm.DeadMansTimeoutMinutes} Minutes";

        WarningCountdownSlider.Value = _vm.WarningCountdownSeconds;
        WarningCountdownLabel.Text = $"{_vm.WarningCountdownSeconds} Seconds";

        PolicyStatusDescriptionText.Text = _vm.EmergencyPolicyStatusDescription;
    }

    private void PolicySettingChanged(object sender, RoutedEventArgs e)
    {
        _vm.IsAutoLockEnabled = AutoLockToggle.IsOn;
        _vm.IsSessionLockEnabled = SessionLockToggle.IsOn;
        _vm.IsDeadMansEnabled = DeadMansToggle.IsOn;
    }

    private void InactivitySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (InactivityTimeoutLabel != null)
        {
            int val = (int)e.NewValue;
            _vm.InactivityTimeoutMinutes = val;
            InactivityTimeoutLabel.Text = $"{val} Minutes";
        }
    }

    private void DeadMansSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (DeadMansTimeoutLabel != null)
        {
            int val = (int)e.NewValue;
            _vm.DeadMansTimeoutMinutes = val;
            DeadMansTimeoutLabel.Text = $"{val} Minutes";
        }
    }

    private void WarningCountdownSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (WarningCountdownLabel != null)
        {
            int val = (int)e.NewValue;
            _vm.WarningCountdownSeconds = val;
            WarningCountdownLabel.Text = $"{val} Seconds";
        }
    }

    private void SavePolicyButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.SaveEmergencyPolicy();
        PolicyStatusDescriptionText.Text = _vm.EmergencyPolicyStatusDescription;
    }

    private void TestCountdownButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.TestEmergencyCountdown();
    }

    private void CancelPendingButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.CancelEmergencyAction();
    }
}
