using Microsoft.Extensions.Logging;

namespace MemoryToolkit.Maui;

public static class AppBuilder
{
    // ReSharper disable once UnusedMethodReturnValue.Global
    public static MauiAppBuilder UseLeakDetection(this MauiAppBuilder builder,
        Action<CollectionTarget>? onLeaked = null,
        Action<CollectionTarget>? onCollected = null,
        IGarbageCollectionMonitor? customMonitor = null)
    {
        if(customMonitor != null)
            GarbageCollectionMonitor.Instance = customMonitor;
        
        GarbageCollectionMonitor.Instance.OnLeaked = onLeaked;
        GarbageCollectionMonitor.Instance.OnCollected = onCollected;

        var logger = builder.Services.BuildServiceProvider()
            .GetService<ILogger<GarbageCollectionMonitor>>();
        if (logger != null)
            GarbageCollectionMonitor.Instance.Logger = logger;

        return builder;
    }
}