using MemoryToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;

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
        
        builder.UseMemoryToolkit(options =>
        {
            options.OnLeaked = collectionTarget =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Page? currentPage = Application.Current?.Windows.FirstOrDefault()?.Page;
                    if (currentPage != null)
                        _ = currentPage.DisplayAlertAsync("Leak Detected",
                            $"{collectionTarget.Name} is a zombie.", "OK");

                    if (Application.Current is App app)
                        app.LeaksDetected++;
                });
            };

            options.DefaultTearDownStrategy = TearDownStrategy.DisconnectHandlers;
        });
#endif

        return builder.Build();
    }
}
