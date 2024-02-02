namespace MemoryToolkit.Maui;

public interface IGCCollectionMonitor
{
    Task ForceCollectionAsync(Action<GCCollectionItem>? onLeaked = null,
        Action<GCCollectionItem>? onCollected = null,
        int maxCollections = 20,
        int msBetweenCollections = 500);

    void Monitor(object target,
        string? name = null,
        Action<GCCollectionItem>? onLeaked = null,
        Action<GCCollectionItem>? onCollected = null);

    Task MonitorAndForceCollectionAsync(List<GCCollectionItem> collectionItems);
    
    void Reset();
}

public class GCCollectionMonitor : IGCCollectionMonitor
{
    private readonly List<GCCollectionItem> _collectionItems = new();

    public static IGCCollectionMonitor Instance { get; } = new GCCollectionMonitor();

    public async Task ForceCollectionAsync(Action<GCCollectionItem>? onLeaked = null,
        Action<GCCollectionItem>? onCollected = null,
        int maxCollections = 25,
        int msBetweenCollections = 100)
    {
        var currentCollection = 0;

        // only check for items that were expected to be collected at the point this run started
        var collectionItems = new List<GCCollectionItem>(_collectionItems);
        
        while (++currentCollection <= maxCollections)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();

            foreach (GCCollectionItem item in collectionItems.ToArray())
            {
                if (item.Reference.IsAlive && currentCollection < maxCollections)
                    continue;

                _collectionItems.Remove(item);

                if (!item.Reference.IsAlive)
                {
                    onCollected?.Invoke(item);
                    item.OnCollected?.Invoke(item);
                }
                else if (currentCollection == maxCollections)
                {
                    onLeaked?.Invoke(item);
                    item.OnLeaked?.Invoke(item);
                }
            }

            await Task.Delay(msBetweenCollections);
        }
    }

    public void Monitor(object target,
        string? name = null,
        Action<GCCollectionItem>? onLeaked = null,
        Action<GCCollectionItem>? onCollected = null)
    {
        _collectionItems.Add(new GCCollectionItem(target,
            name,
            onLeaked,
            onCollected
        ));
    }

    public async Task MonitorAndForceCollectionAsync(List<GCCollectionItem> collectionItems)
    {
        const int maxCollections = 10;
        const int msBetweenCollections = 200;
        var currentCollection = 0;
        
        while (++currentCollection <= maxCollections)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();

            foreach (GCCollectionItem item in collectionItems.ToArray())
            {
                if (item.Reference.IsAlive && currentCollection < maxCollections)
                    continue;

                collectionItems.Remove(item);

                if (!item.Reference.IsAlive)
                {
                    item.OnCollected?.Invoke(item);
                }
                else if (currentCollection == maxCollections)
                {
                    item.OnLeaked?.Invoke(item);
                }
            }

            await Task.Delay(msBetweenCollections);
        }
    }

    public void Reset()
    {
        _collectionItems.Clear();
    }
}