using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

[DllImport("user32.dll", CharSet = CharSet.Unicode)]
static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

try
{
    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
    string[] possiblePaths = new[]
    {
        Path.Combine(baseDir, "publish", "PhantomVault.App.exe"),
        Path.Combine(baseDir, "src", "PhantomVault.App", "bin", "x64", "Release", "net8.0-windows10.0.26100.0", "win-x64", "PhantomVault.App.exe"),
        Path.Combine(baseDir, "src", "PhantomVault.App", "bin", "Release", "net8.0-windows10.0.26100.0", "win-x64", "PhantomVault.App.exe"),
        Path.Combine(baseDir, "src", "PhantomVault.App", "bin", "x64", "Debug", "net8.0-windows10.0.26100.0", "win-x64", "PhantomVault.App.exe"),
        Path.Combine(baseDir, "src", "PhantomVault.App", "bin", "Debug", "net8.0-windows10.0.26100.0", "win-x64", "PhantomVault.App.exe")
    };

    string? targetExe = null;
    foreach (var p in possiblePaths)
    {
        if (File.Exists(p))
        {
            targetExe = p;
            break;
        }
    }

    if (targetExe != null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = targetExe,
            WorkingDirectory = Path.GetDirectoryName(targetExe)!,
            UseShellExecute = true
        };
        Process.Start(psi);
    }
    else
    {
        MessageBox(IntPtr.Zero,
            "PHANTOMVAULT application binaries were not found.\nPlease make sure 'publish\\PhantomVault.App.exe' exists.",
            "PHANTOMVAULT",
            0x10);
    }
}
catch (Exception ex)
{
    MessageBox(IntPtr.Zero,
        "Error launching PHANTOMVAULT:\n" + ex.Message,
        "PHANTOMVAULT Launcher Error",
        0x10);
}
