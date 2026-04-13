namespace MemoryToolkit.Maui.Tests;

public sealed class GarbageCollectionMonitorTests
{
    [Fact]
    public async Task MonitorReportsCollectedTargets()
    {
        var collectedTargets = new List<string>();
        var leakedTargets = new List<string>();
        var monitor = new GarbageCollectionMonitor
        {
            MaxCollections = 3,
            MillisecondsBetweenCollections = 1,
            OnCollected = target => collectedTargets.Add(target.Name),
            OnLeaked = target => leakedTargets.Add(target.Name)
        };

        CollectionTarget target = CreateCollectableTarget();

        await monitor.MonitorAndForceCollectionAsync([target]);

        Assert.Contains("collectable", collectedTargets);
        Assert.Empty(leakedTargets);
    }

    [Fact]
    public async Task MonitorReportsLeakedTargets()
    {
        var collectedTargets = new List<string>();
        var leakedTargets = new List<string>();
        var monitor = new GarbageCollectionMonitor
        {
            MaxCollections = 2,
            MillisecondsBetweenCollections = 1,
            OnCollected = target => collectedTargets.Add(target.Name),
            OnLeaked = target => leakedTargets.Add(target.Name)
        };
        var retainedObject = new object();

        await monitor.MonitorAndForceCollectionAsync([new CollectionTarget(retainedObject, "retained")]);

        GC.KeepAlive(retainedObject);
        Assert.Empty(collectedTargets);
        Assert.Contains("retained", leakedTargets);
    }

    private static CollectionTarget CreateCollectableTarget()
    {
        return new CollectionTarget(new object(), "collectable");
    }
}
