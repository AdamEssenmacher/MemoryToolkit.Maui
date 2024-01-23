using Microsoft.Extensions.Logging;

namespace MemoryToolkit.Maui.Demos.MainPage;

public partial class App
{
    public App(ILogger<App> logger)
        : base(logger)
    {
        InitializeComponent();

        MainPage = new NavigationPage(new MainPage());
    }
}