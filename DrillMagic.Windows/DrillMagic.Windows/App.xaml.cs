using DrillMagic.Windows.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System;

namespace DrillMagic.Windows;

public partial class App : Application
{
    public static new App Current => (App)Application.Current;
    public IServiceProvider Services { get; }
    public static Window? MainWindow { get; private set; }

    public App()
    {
        Services = ConfigureServices();
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = Services.GetRequiredService<MainWindow>();
        MainWindow.Activate();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Services
        services.AddSingleton<IDrillGridManager, DrillGridManager>();
        services.AddSingleton<SharedInteractionService>();

        // Views
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}