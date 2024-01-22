namespace MemoryToolkit.Maui;

public class GCCollectionItem
{
    public GCCollectionItem(object reference,
        string? name = null,
        Action<GCCollectionItem>? onLeaked = null,
        Action<GCCollectionItem>? onCollected = null)
    {
        Reference = new WeakReference(reference);
        Name = name ?? reference.GetType().Name;
        OnLeaked = onLeaked;
        OnCollected = onCollected;
    }

    public string Name { get; set; }
    public WeakReference Reference { get; }
    public Action<GCCollectionItem>? OnLeaked { get; set; }
    public Action<GCCollectionItem>? OnCollected { get; set; }
}