namespace MemoryToolkit.Maui;

public sealed class MemoryToolkitOptions
{
    public TearDownStrategy DefaultTearDownStrategy { get; set; } = TearDownStrategy.DisconnectHandlers;

    public Action<CollectionTarget>? OnLeaked { get; set; }

    public Action<CollectionTarget>? OnCollected { get; set; }

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
