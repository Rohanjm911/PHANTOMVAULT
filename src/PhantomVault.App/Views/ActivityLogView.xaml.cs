using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PhantomVault_App.ViewModels;

namespace PhantomVault_App.Views;

public sealed partial class ActivityLogView : Page
{
    private readonly MainViewModel _vm;

    public ActivityLogView()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _vm;
        Loaded += async (s, e) =>
        {
            await _vm.RefreshActivityLogsAsync();
            LogsListView.ItemsSource = _vm.ActivityLogs;
        };
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await _vm.RefreshActivityLogsAsync();
    }

    private async void ClearLogsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Clear Audit History?",
            Content = "This will permanently purge all local activity log records.",
            PrimaryButtonText = "Clear History",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await _vm.ClearActivityLogsAsync();
        }
    }
}
