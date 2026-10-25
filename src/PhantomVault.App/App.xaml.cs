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
            System.IO.File.WriteAllText("crash.log", $"{e.Message}\n{e.Exception}");
        };
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            System.IO.File.WriteAllText("domain_crash.log", $"{e.ExceptionObject}");
        };
        Services = ConfigureServices();
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
