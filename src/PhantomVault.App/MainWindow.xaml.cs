using Microsoft.UI.Xaml;

namespace PhantomVault_App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Title = "PHANTOMVAULT — Trust Nothing. Reveal Nothing.";

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        PhantomVault_App.Helpers.FilePickerHelper.MainWindowHandle = hwnd;

        // Navigate root frame to MainPage
        RootFrame.Navigate(typeof(MainPage));
    }
}
