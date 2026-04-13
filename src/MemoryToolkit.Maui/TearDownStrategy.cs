namespace MemoryToolkit.Maui;

/// <summary>
/// Describes how MemoryToolkit.Maui should tear down a view when it appears to be done.
/// </summary>
public enum TearDownStrategy
{
    /// <summary>
    /// Do not apply teardown. Use this when you only want leak detection or manual cleanup.
    /// </summary>
    DetectOnly,

    /// <summary>
    /// Use MAUI's built-in handler disconnection path without clearing managed object graph references.
    /// </summary>
    DisconnectHandlers,

    /// <summary>
    /// Clear managed references such as binding contexts, content references, item sources, logical children, and resources before disconnecting handlers.
    /// </summary>
    Compartmentalize
}
