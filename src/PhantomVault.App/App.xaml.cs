using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using PhantomVault.Core.Interfaces;
using PhantomVault.Infrastructure.Backup;
using PhantomVault.Infrastructure.Logging;
using PhantomVault.Security.Cryptography;
using PhantomVault.Security.KeyManagement;
using PhantomVault.Security.PasswordProtection;
using PhantomVault.Security.SecurityPolicies;
using PhantomVault.Steganography;
using PhantomVault.Storage;

namespace PhantomVault_App;

/// <summary>
/// Provides application-specific behavior and service resolution for PHANTOMVAULT.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        InitializeComponent();

        UnhandledException += (sender, e) =>
        {
            LogCrash("UnhandledException", e.Exception, e.Message);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogCrash("AppDomain.UnhandledException", ex, ex.Message);
            }
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            LogCrash("UnobservedTaskException", e.Exception, e.Exception.Message);
            e.SetObserved();
        };

        Services = ConfigureServices();
    }

    private static void LogCrash(string source, Exception? ex, string message)
    {
        try
        {
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PHANTOMVAULT");
            if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
            string logPath = Path.Combine(logDir, "crash_log.txt");
            string entry = $"[{DateTime.UtcNow:O}] [{source}] {message}\n{ex}\n{new string('-', 60)}\n";
            File.AppendAllText(logPath, entry);
            System.Diagnostics.Debug.WriteLine($"[CRASH LOGGED] {entry}");
        }
        catch { }
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Cryptography & Key Management
        services.AddSingleton<ICryptographyService, AesGcmCryptoProvider>();
        services.AddSingleton<IKeyDerivationService, KeyDerivationService>();
        services.AddSingleton<IKeyWrappingService, KeyWrappingService>();
        services.AddSingleton<IPasswordPolicyValidator, PasswordPolicyValidator>();
        services.AddSingleton<ISecurityPolicyService, SecurityPolicyService>();

        // Storage & Repositories
        services.AddSingleton<IVaultRepository, VaultRepository>();
        services.AddSingleton<IVaultFileManager, VaultFileManager>();

        // Steganography & Backup
        services.AddSingleton<ISteganographyService, SteganographyService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IActivityLogService, ActivityLogService>();

        // Advanced Security Defenses & Authentication
        services.AddSingleton<ISecureShredderService, PhantomVault.Security.Shredding.FileShredderService>();
        services.AddSingleton<IWindowsHelloService, Services.WindowsHelloService>();

        // Tactical Audio Synthesis & Plausible Deniability
        services.AddSingleton<ISoundEffectService, Services.SoundEffectService>();
        services.AddSingleton<IDecoyVaultService, PhantomVault.Infrastructure.Decoy.DecoyVaultService>();

        // ViewModels
        services.AddSingleton<ViewModels.MainViewModel>();

        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
