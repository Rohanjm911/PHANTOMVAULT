using System;
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
        Loaded += ActivityLogView_Loaded;
    }

    private async void ActivityLogView_Loaded(object sender, RoutedEventArgs e)
    {
        await _vm.RefreshActivityLogsAsync();
        LogsListView.ItemsSource = _vm.FilteredActivityLogs;
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await _vm.RefreshActivityLogsAsync();
    }

    private async void ClearLogsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Clear Audit History",
            Content = "Are you sure you want to permanently clear the local activity audit trail? This will wipe all recorded session events from the local SQLite database.",
            PrimaryButtonText = "Clear History",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await _vm.ClearActivityLogsAsync();
        }
    }

    private void FilterCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FilterCategoryComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
        {
            _vm.SelectedLogCategory = item.Tag.ToString() ?? "All";
        }
    }

    private void SearchLogBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _vm.LogSearchQuery = SearchLogBox.Text;
    }
}
