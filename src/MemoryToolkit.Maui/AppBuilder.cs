using Microsoft.Extensions.Logging;

namespace MemoryToolkit.Maui;

public static class AppBuilder
{
    // ReSharper disable once UnusedMethodReturnValue.Global
    public static MauiAppBuilder UseMemoryToolkit(this MauiAppBuilder builder,
        Action<MemoryToolkitOptions>? configure = null)
    {
        var options = new MemoryToolkitOptions();
        configure?.Invoke(options);
        MemoryToolkitConfiguration.Configure(options);

        if (options.CustomMonitor != null)
            GarbageCollectionMonitor.Instance = options.CustomMonitor;

        GarbageCollectionMonitor.Instance.OnLeaked = options.OnLeaked;
        GarbageCollectionMonitor.Instance.OnCollected = options.OnCollected;

        var logger = builder.Services.BuildServiceProvider()
            .GetService<ILogger<GarbageCollectionMonitor>>();
        if (logger != null)
            GarbageCollectionMonitor.Instance.Logger = logger;

        return builder;
    }

    // ReSharper disable once UnusedMethodReturnValue.Global
    [Obsolete("Use UseMemoryToolkit instead.")]
    public static MauiAppBuilder UseLeakDetection(this MauiAppBuilder builder,
        Action<CollectionTarget>? onLeaked = null,
        Action<CollectionTarget>? onCollected = null,
        IGarbageCollectionMonitor? customMonitor = null)
    {
        return builder.UseMemoryToolkit(options =>
        {
            options.OnLeaked = onLeaked;
            options.OnCollected = onCollected;
            options.CustomMonitor = customMonitor;
        });
    }
}
