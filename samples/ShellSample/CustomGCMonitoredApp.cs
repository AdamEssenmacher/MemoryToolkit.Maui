using MemoryToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace ShellSample;

public class CustomGCMonitoredApp(ILogger<GCMonitoredApplication> logger) : GCMonitoredApplication(logger)
{
    public int LeaksDetected { get; private set; }

    public override void OnLeaked(GCCollectionItem item)
    {
        base.OnLeaked(item);
        LeaksDetected++;
    }
}