using Microsoft.Extensions.Logging;

namespace MemoryToolkit.Maui.E2ETests;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMemoryToolkit(options =>
            {
                options.DefaultTearDownStrategy = TearDownStrategy.DisconnectHandlers;
            });

        builder.Logging.AddDebug();

        return builder.Build();
    }
}
