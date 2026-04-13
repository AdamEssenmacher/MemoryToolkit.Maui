namespace MemoryToolkit.Maui;

/// <summary>
/// Configures MemoryToolkit.Maui V2 behavior for leak monitoring and teardown.
/// </summary>
public sealed class MemoryToolkitOptions
{
    /// <summary>
    /// Gets or sets the teardown strategy used when <see cref="TearDownBehavior"/> runs without an explicit attached strategy.
    /// </summary>
    /// <remarks>
    /// The V2 default is <see cref="TearDownStrategy.DisconnectHandlers"/>, which delegates to MAUI's built-in handler disconnection path.
    /// </remarks>
    public TearDownStrategy DefaultTearDownStrategy { get; set; } = TearDownStrategy.DisconnectHandlers;

    /// <summary>
    /// Gets or sets a callback invoked when a monitored target remains alive after repeated forced garbage collections.
    /// </summary>
    public Action<CollectionTarget>? OnLeaked { get; set; }

    /// <summary>
    /// Gets or sets a callback invoked when a monitored target is collected.
    /// </summary>
    public Action<CollectionTarget>? OnCollected { get; set; }

    /// <summary>
    /// Gets or sets a custom monitor used instead of the default <see cref="GarbageCollectionMonitor"/>.
    /// </summary>
    public IGarbageCollectionMonitor? CustomMonitor { get; set; }
}

internal static class MemoryToolkitConfiguration
{
    public static MemoryToolkitOptions Options { get; private set; } = new();

    public static void Configure(MemoryToolkitOptions options)
    {
        Options = options;
    }
}
