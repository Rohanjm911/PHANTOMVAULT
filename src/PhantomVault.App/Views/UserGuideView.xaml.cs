using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PhantomVault_App.ViewModels;

namespace PhantomVault_App.Views;

public sealed partial class UserGuideView : Page
{
    private readonly MainViewModel _vm;
    private string _currentCategory = "All";
    private readonly List<Border> _allCards = new();

    public UserGuideView()
    {
        _vm = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _vm;
        InitializeComponent();

        Loaded += UserGuideView_Loaded;
    }

    private void UserGuideView_Loaded(object sender, RoutedEventArgs e)
    {
        _allCards.Clear();
        foreach (var child in GuidesPanel.Children)
        {
            if (child is Border card)
            {
                _allCards.Add(card);
            }
        }
    }

    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            _currentCategory = tag;
            UpdateFilterButtonStyles(tag);
            ApplyFilters();
        }
    }

    private void UpdateFilterButtonStyles(string activeTag)
    {
        var primaryStyle = (Style)Application.Current.Resources["PrimaryButtonStyle"];
        var secondaryStyle = (Style)Application.Current.Resources["SecondaryButtonStyle"];

        FilterAllButton.Style = activeTag == "All" ? primaryStyle : secondaryStyle;
        FilterVaultsButton.Style = activeTag == "Vaults" ? primaryStyle : secondaryStyle;
        FilterDecoyButton.Style = activeTag == "Decoy" ? primaryStyle : secondaryStyle;
        FilterStegoButton.Style = activeTag == "Stego" ? primaryStyle : secondaryStyle;
        FilterShredderButton.Style = activeTag == "Shredder" ? primaryStyle : secondaryStyle;
        FilterDefenseButton.Style = activeTag == "Defense" ? primaryStyle : secondaryStyle;
        FilterHotkeysButton.Style = activeTag == "Hotkeys" ? primaryStyle : secondaryStyle;
    }

    private void SearchGuideBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        string query = SearchGuideBox.Text?.Trim().ToLowerInvariant() ?? string.Empty;

        foreach (var card in _allCards)
        {
            string cardTag = card.Tag?.ToString() ?? string.Empty;
            bool matchesCategory = _currentCategory == "All" || cardTag.Equals(_currentCategory, StringComparison.OrdinalIgnoreCase);

            bool matchesQuery = string.IsNullOrEmpty(query);
            if (!matchesQuery)
            {
                // Inspect all text in card
                string cardText = ExtractText(card).ToLowerInvariant();
                matchesQuery = cardText.Contains(query);
            }

            card.Visibility = (matchesCategory && matchesQuery) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private string ExtractText(FrameworkElement element)
    {
        var sb = new System.Text.StringBuilder();
        CollectText(element, sb);
        return sb.ToString();
    }

    private void CollectText(DependencyObject obj, System.Text.StringBuilder sb)
    {
        if (obj is TextBlock tb && !string.IsNullOrEmpty(tb.Text))
        {
            sb.Append(' ').Append(tb.Text);
        }
        else if (obj is Button btn && btn.Content is string s)
        {
            sb.Append(' ').Append(s);
        }

        int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(obj);
        for (int i = 0; i < count; i++)
        {
            CollectText(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(obj, i), sb);
        }
    }

    #region Navigation Jumps
    private void NavToVaults_Click(object sender, RoutedEventArgs e)
    {
        _vm.RequestNavigate("Vaults");
    }

    private void NavToWorkspace_Click(object sender, RoutedEventArgs e)
    {
        _vm.RequestNavigate("Vault");
    }

    private void NavToStego_Click(object sender, RoutedEventArgs e)
    {
        _vm.RequestNavigate("Stego");
    }

    private void NavToShredder_Click(object sender, RoutedEventArgs e)
    {
        _vm.RequestNavigate("Vault");
    }

    private void NavToEmergency_Click(object sender, RoutedEventArgs e)
    {
        _vm.RequestNavigate("Emergency");
    }

    private void NavToSettings_Click(object sender, RoutedEventArgs e)
    {
        _vm.RequestNavigate("Settings");
    }
    #endregion
}
