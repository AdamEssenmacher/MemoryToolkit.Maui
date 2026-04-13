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

    public int MaxCollections { get; set; } = 10;

    public int MillisecondsBetweenCollections { get; set; } = 200;

    public async Task MonitorAndForceCollectionAsync(List<CollectionTarget> collectionItems)
    {
        var currentCollection = 0;

        while (++currentCollection <= MaxCollections)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();

            foreach (CollectionTarget item in collectionItems.ToArray())
            {
                if (item.Reference.IsAlive && currentCollection < MaxCollections)
                    continue;

                collectionItems.Remove(item);

                if (!item.Reference.IsAlive)
                    OnCollectedInternal(item);
                else if (currentCollection == MaxCollections) OnLeakedInternal(item);
            }

            await Task.Delay(MillisecondsBetweenCollections);
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
