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
    /// Clear managed references such as binding contexts, MAUI behaviors, content references, item sources, item templates, gesture recognizers, formatted text spans, logical children, and resources before disconnecting handlers. Reference clearing is best-effort, so failures are logged and teardown continues.
    /// </summary>
    Compartmentalize
}
