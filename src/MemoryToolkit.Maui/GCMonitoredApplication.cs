using Microsoft.Extensions.Logging;

// ReSharper disable VirtualMemberNeverOverridden.Global
// ReSharper disable MemberCanBeProtected.Global

namespace MemoryToolkit.Maui;

public class GCMonitoredApplication(ILogger<GCMonitoredApplication> logger) : Application
{
    public static readonly BindableProperty ShowMemToolkitAlertsProperty =
        BindableProperty.Create(nameof(ShowMemToolkitAlerts), typeof(bool), typeof(GCMonitoredApplication), true);

    protected Page? CurrentPage { get; set; }

    public bool ShowMemToolkitAlerts
    {
        get => (bool)GetValue(ShowMemToolkitAlertsProperty);
        set => SetValue(ShowMemToolkitAlertsProperty, value);
    }

    public ILogger<GCMonitoredApplication> Logger { get; set; } = logger;

    public virtual void OnLeaked(GCCollectionItem item)
    {
        Logger.LogWarning("❗🧟❗{TargetName} is a zombie", item.Name);

        if (ShowMemToolkitAlerts)
            CurrentPage?.DisplayAlert("💦Leak Detected💦", $"❗🧟❗{item.Name} is a zombie!", "OK");
    }

    public virtual void OnCollected(GCCollectionItem item)
    {
        Logger.LogTrace("✅{TargetName} released", item.Name);
    }
}