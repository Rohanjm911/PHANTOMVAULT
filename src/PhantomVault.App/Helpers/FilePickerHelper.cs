using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PhantomVault_App.Helpers;

public static class FilePickerHelper
{
    public static IntPtr MainWindowHandle { get; set; }

    public static async Task<string?> PickSingleFileAsync(params string[] filterExtensions)
    {
        var picker = new FileOpenPicker();
        if (MainWindowHandle != IntPtr.Zero)
        {
            InitializeWithWindow.Initialize(picker, MainWindowHandle);
        }

        picker.ViewMode = PickerViewMode.List;
        if (filterExtensions != null && filterExtensions.Length > 0)
        {
            foreach (var ext in filterExtensions)
            {
                picker.FileTypeFilter.Add(ext);
            }
        }
        else
        {
            picker.FileTypeFilter.Add("*");
        }

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    public static async Task<string?> PickSaveFileAsync(string defaultFileName, string extension, string fileTypeDescription)
    {
        var picker = new FileSavePicker();
        if (MainWindowHandle != IntPtr.Zero)
        {
            InitializeWithWindow.Initialize(picker, MainWindowHandle);
        }

        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.SuggestedFileName = defaultFileName;
        picker.FileTypeChoices.Add(fileTypeDescription, new List<string> { extension });

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    public static async Task<string?> PickSingleFolderAsync()
    {
        var picker = new FolderPicker();
        if (MainWindowHandle != IntPtr.Zero)
        {
            InitializeWithWindow.Initialize(picker, MainWindowHandle);
        }

        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add("*");

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
