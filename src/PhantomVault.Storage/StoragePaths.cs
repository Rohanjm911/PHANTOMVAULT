using System;
using System.IO;

namespace PhantomVault.Storage;

/// <summary>
/// Centralized management of application storage paths.
/// </summary>
public static class StoragePaths
{
    private static readonly string BaseDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
        "PHANTOMVAULT");

    public static string AppDataDirectory
    {
        get
        {
            if (!Directory.Exists(BaseDirectory))
            {
                Directory.CreateDirectory(BaseDirectory);
            }
            return BaseDirectory;
        }
    }

    public static string DefaultVaultsDirectory
    {
        get
        {
            string path = Path.Combine(AppDataDirectory, "Vaults");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }
    }

    public static string VaultRegistryPath => Path.Combine(AppDataDirectory, "vaults_registry.json");
    public static string ActivityLogPath => Path.Combine(AppDataDirectory, "activity_history.db");
    public static string SettingsPath => Path.Combine(AppDataDirectory, "app_settings.json");
}
