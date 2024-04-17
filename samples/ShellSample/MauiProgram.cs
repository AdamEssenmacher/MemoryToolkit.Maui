using MemoryToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace ShellSample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
        
        builder.UseLeakDetection(collectionTarget =>
        {
            Application.Current?.MainPage?.DisplayAlert("💦Leak Detected💦",
                $"❗🧟❗{collectionTarget.Name} is a zombie!", "OK");

            ((App)Application.Current!).LeaksDetected++;
        });
#endif

        return builder.Build();
    }
}