using DrillMagic.Core;
using DrillMagic.Core.Types;
using DrillMagic.Windows.Services;
using DrillMagic.Windows.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System;

namespace DrillMagic.Windows;

public partial class App : Application
{
    public static new App Current => (App)Application.Current;
    public IServiceProvider? Services { get; private set; }
    public static Window? MainWindow { get; private set; }


    public App()
    {
        this.InitializeComponent();
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