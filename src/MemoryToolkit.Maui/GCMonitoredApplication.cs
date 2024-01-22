using System.ComponentModel;
using Microsoft.Extensions.Logging;

// ReSharper disable VirtualMemberNeverOverridden.Global
// ReSharper disable MemberCanBeProtected.Global

namespace MemoryToolkit.Maui;

public class GCMonitoredApplication : Application
{
    private readonly IGCCollectionMonitor _collectionMonitor;
    private readonly ILogger<GCMonitoredApplication> _logger;
    private Page? _currentPage;

    public GCMonitoredApplication(IGCCollectionMonitor collectionMonitor,
        ILogger<GCMonitoredApplication> logger)
    {
        _collectionMonitor = collectionMonitor;
        _logger = logger;
        
        PropertyChanged += HandleMainPageChanged;
    }

    protected virtual void OnLeaked(GCCollectionItem item)
    {
        _logger.LogWarning("❗🧟❗{TargetName} is a zombie", item.Name);
        _currentPage?.DisplayAlert("💦Leak Detected💦", $"❗🧟❗{item.Name} is a zombie!", "OK");
    }

    protected virtual void OnCollected(GCCollectionItem item)
    {
        _logger.LogTrace("✅{TargetName} released", item.Name);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Window window = base.CreateWindow(activationState);
        _currentPage = window.Page;
        window.PropertyChanged += HandleWindowPageChanged;

        return window;
    }

    public override void CloseWindow(Window window)
    {
        _currentPage = null;
        window.PropertyChanged -= HandleWindowPageChanged;
        base.CloseWindow(window);
    }

    private void HandleWindowPageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Window window)
            return;

        if (e.PropertyName != nameof(Window.Page))
            return;

        Page? lastPage = _currentPage;
        if (lastPage is NavigationPage lastNavPage)
            lastNavPage.Popped -= NavPageOnPopped;

        _currentPage = window.Page;
        if (_currentPage is NavigationPage currentNavPage)
            currentNavPage.Popped += NavPageOnPopped;

        _collectionMonitor.ForceCollectionAsync(OnLeaked, OnCollected);
    }
    
    private void HandleMainPageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainPage))
            return;

        Page? lastPage = _currentPage;
        if (lastPage is NavigationPage lastNavPage)
            lastNavPage.Popped -= NavPageOnPopped;
        
        _currentPage = MainPage;
        if(_currentPage is NavigationPage currentNavPage)
            currentNavPage.Popped += NavPageOnPopped;

        _collectionMonitor.ForceCollectionAsync(OnLeaked, OnCollected);
    }

    private void NavPageOnPopped(object? sender, NavigationEventArgs e)
    {
        _collectionMonitor.ForceCollectionAsync(OnLeaked, OnCollected);
    }
}