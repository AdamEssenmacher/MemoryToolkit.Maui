using Microsoft.Extensions.Logging;

namespace ShellSample;

public partial class App
{
    public App(ILogger<App> logger) : base(logger)
    {
        InitializeComponent();

        MainPage = new AppShell();
    }
}