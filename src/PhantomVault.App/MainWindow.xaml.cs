using System;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace PhantomVault_App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Title = "PHANTOMVAULT — Trust Nothing. Reveal Nothing.";

        try
        {
            if (MicaController.IsSupported())
            {
                SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
            }
        }
        catch
        {
            // Fallback gracefully on Windows 10
        }

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        PhantomVault_App.Helpers.FilePickerHelper.MainWindowHandle = hwnd;

        // Navigate root frame to MainPage
        RootFrame.Navigate(typeof(MainPage));
    }
}
