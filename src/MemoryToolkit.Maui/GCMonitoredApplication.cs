using System.ComponentModel;
using Microsoft.Extensions.Logging;

// ReSharper disable VirtualMemberNeverOverridden.Global
// ReSharper disable MemberCanBeProtected.Global

namespace MemoryToolkit.Maui;

public class GCMonitoredApplication : Application
{
    public static readonly BindableProperty ShowMemToolkitAlertsProperty =
        BindableProperty.Create(nameof(ShowMemToolkitAlerts), typeof(bool), typeof(GCMonitoredApplication), true);

    public GCMonitoredApplication(ILogger<GCMonitoredApplication> logger)
    {
        CollectionMonitor = GCCollectionMonitor.Instance;
        Logger = logger;

        PropertyChanged += HandleMainPageChanged;
    }

    protected IGCCollectionMonitor CollectionMonitor { get; set; }
    protected Page? CurrentPage { get; set; }

    public bool ShowMemToolkitAlerts
    {
        get => (bool)GetValue(ShowMemToolkitAlertsProperty);
        set => SetValue(ShowMemToolkitAlertsProperty, value);
    }

    public ILogger<GCMonitoredApplication> Logger { get; set; }

    protected virtual void OnLeaked(GCCollectionItem item)
    {
        Logger.LogWarning("❗🧟❗{TargetName} is a zombie", item.Name);

        if (ShowMemToolkitAlerts)
            CurrentPage?.DisplayAlert("💦Leak Detected💦", $"❗🧟❗{item.Name} is a zombie!", "OK");
    }

    protected virtual void OnCollected(GCCollectionItem item)
    {
        Logger.LogTrace("✅{TargetName} released", item.Name);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Window window = base.CreateWindow(activationState);
        CurrentPage = window.Page;
        window.PropertyChanged += HandleWindowPageChanged;

        return window;
    }

    public override void CloseWindow(Window window)
    {
        CurrentPage = null;
        window.PropertyChanged -= HandleWindowPageChanged;
        base.CloseWindow(window);
    }

    protected void HandleWindowPageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Window window)
            return;

        if (e.PropertyName != nameof(Window.Page))
            return;

        Page? lastPage = CurrentPage;
        if (lastPage is NavigationPage lastNavPage)
            lastNavPage.Popped -= NavPageOnPopped;

        CurrentPage = window.Page;
        if (CurrentPage is NavigationPage currentNavPage)
            currentNavPage.Popped += NavPageOnPopped;

        CollectionMonitor.ForceCollectionAsync(OnLeaked, OnCollected);
    }

    protected void HandleMainPageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainPage))
            return;

        Page? lastPage = CurrentPage;
        if (lastPage is NavigationPage lastNavPage)
            lastNavPage.Popped -= NavPageOnPopped;

        CurrentPage = MainPage;
        if (CurrentPage is NavigationPage currentNavPage)
            currentNavPage.Popped += NavPageOnPopped;

        CollectionMonitor.ForceCollectionAsync(OnLeaked, OnCollected);
    }

    protected void NavPageOnPopped(object? sender, NavigationEventArgs e)
    {
        CollectionMonitor.ForceCollectionAsync(OnLeaked, OnCollected);
    }
}