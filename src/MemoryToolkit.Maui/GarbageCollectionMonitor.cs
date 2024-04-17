using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MemoryToolkit.Maui;

public interface IGarbageCollectionMonitor
{
    // ReSharper disable once UnusedMemberInSuper.Global
    Action<CollectionTarget>? OnCollected { get; set; }
    // ReSharper disable once UnusedMemberInSuper.Global
    Action<CollectionTarget>? OnLeaked { get; set; }
    // ReSharper disable once UnusedMemberInSuper.Global
    public ILogger Logger { get; set; }
    Task MonitorAndForceCollectionAsync(List<CollectionTarget> collectionItems);
}

// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class GarbageCollectionMonitor : IGarbageCollectionMonitor
{
    // ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
    public static IGarbageCollectionMonitor Instance { get; set; } = new GarbageCollectionMonitor();
    // ReSharper restore AutoPropertyCanBeMadeGetOnly.Global

    public ILogger Logger { get; set; } = NullLogger.Instance;

    public Action<CollectionTarget>? OnLeaked { get; set; }

    public Action<CollectionTarget>? OnCollected { get; set; }

    public async Task MonitorAndForceCollectionAsync(List<CollectionTarget> collectionItems)
    {
        const int maxCollections = 10;
        const int msBetweenCollections = 200;
        var currentCollection = 0;

        while (++currentCollection <= maxCollections)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();

            foreach (CollectionTarget item in collectionItems.ToArray())
            {
                if (item.Reference.IsAlive && currentCollection < maxCollections)
                    continue;

                collectionItems.Remove(item);

                if (!item.Reference.IsAlive)
                    OnCollectedInternal(item);
                else if (currentCollection == maxCollections) OnLeakedInternal(item);
            }

            await Task.Delay(msBetweenCollections);
        }
    }

    protected virtual void OnLeakedInternal(CollectionTarget target)
    {
        Logger.LogWarning("❗🧟❗{TargetName} is a zombie", target.Name);
        OnLeaked?.Invoke(target);
    }

    private protected virtual void OnCollectedInternal(CollectionTarget target)
    {
        Logger.LogInformation("✅{TargetName} released", target.Name);
        OnCollected?.Invoke(target);
    }
}