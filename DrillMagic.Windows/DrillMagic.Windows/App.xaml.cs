using DrillMagic.Core;
using DrillMagic.Core.Types;
using DrillMagic.Windows.Services;
using DrillMagic.Windows.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;

namespace DrillMagic.Windows;

public partial class App : Application
{
    public static new App Current => (App)Application.Current;
    public IServiceProvider? Services { get; private set; }
    public static Window? MainWindow { get; private set; }


    public App()
    {
        this.InitializeComponent();
        ConfigureLogging();
    }

    private void ConfigureLogging()
    {
        // For packaged apps, use the LocalFolder from ApplicationData to ensure consistency
        // and correct virtualization handling.
        string logFolder;
        try
        {
             logFolder = Path.Combine(global::Windows.Storage.ApplicationData.Current.LocalFolder.Path, "DrillMagicLogs");
        }
        catch (InvalidOperationException)
        {
            // Fallback for unpackaged runs or if ApplicationData is unavailable
            logFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DrillMagic");
        }
        
        // Ensure the directory exists
        Directory.CreateDirectory(logFolder);
        
        var logPath = Path.Combine(logFolder, "logs.txt");

        // Print the path to the Output window so you can find it easily
        Debug.WriteLine($"[DrillMagic] Logging to: {logPath}");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Debug()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();
            
        Log.Information("Logging initialized at {LogPath}", logPath);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // We revert to a standard, clean startup sequence.
        // 1. Configure services.
        Services = ConfigureServices();

        // 2. Create the main window.
        MainWindow = new MainWindow();

        // 3. Activate the window. The MainWindow's Loaded event will handle the rest.
        MainWindow.Activate();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));

        // Core Services
        services.AddSingleton<IColorMapRepository, ColorMapRepository>();
        services.AddSingleton<IColorMapService, ColorMapService>();
        
        // App Services
        services.AddSingleton<IDrillGridManager, DrillGridManager>();
        services.AddSingleton<SharedInteractionService>();
        services.AddSingleton<ISharedInteractionService>(sp => sp.GetRequiredService<SharedInteractionService>());
        
        // Utils
        services.AddSingleton<IPdfGenerator, PdfSharpGenerator>();
        services.AddSingleton<IDrillGridFactory, DrillGridFactory>();

        return services.BuildServiceProvider();
    }
}