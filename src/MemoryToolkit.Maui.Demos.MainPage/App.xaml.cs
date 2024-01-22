using Microsoft.Extensions.Logging;

namespace MemoryToolkit.Maui.Demos.MainPage;

public partial class App
{
    public App(IGCCollectionMonitor gcCollectionMonitor, ILogger<App> logger)
        : base(gcCollectionMonitor, logger)
    {
        InitializeComponent();

        MainPage = new NavigationPage(new MainPage());
    }
}