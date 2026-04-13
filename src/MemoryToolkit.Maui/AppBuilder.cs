using Microsoft.Extensions.Logging;

namespace MemoryToolkit.Maui;

public static class AppBuilder
{
    // ReSharper disable once UnusedMethodReturnValue.Global
    /// <summary>
    /// Configures MemoryToolkit.Maui services and V2 defaults for the current MAUI app builder.
    /// </summary>
    /// <param name="builder">The MAUI app builder to configure.</param>
    /// <param name="configure">An optional callback used to customize leak callbacks, teardown strategy, and monitor behavior.</param>
    /// <returns>The same <see cref="MauiAppBuilder"/> instance so calls can be chained.</returns>
    /// <remarks>
    /// Call this after logging has been configured if you want toolkit leak logs to use the app's logger configuration.
    /// </remarks>
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
    /// <summary>
    /// Configures leak detection using the V1 compatibility entrypoint.
    /// </summary>
    /// <param name="builder">The MAUI app builder to configure.</param>
    /// <param name="onLeaked">An optional callback invoked when a monitored target remains alive after forced collections.</param>
    /// <param name="onCollected">An optional callback invoked when a monitored target is collected.</param>
    /// <param name="customMonitor">An optional custom garbage collection monitor implementation.</param>
    /// <returns>The same <see cref="MauiAppBuilder"/> instance so calls can be chained.</returns>
    /// <remarks>
    /// This method is retained for source compatibility. New apps should use <see cref="UseMemoryToolkit"/>.
    /// </remarks>
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
