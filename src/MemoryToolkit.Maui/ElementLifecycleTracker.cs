namespace MemoryToolkit.Maui;

internal sealed record LifecycleAction(
    string Key,
    Func<BindableObject, bool> IsSuppressed,
    Action<VisualElement> OnDone);

internal static class ElementLifecycleTracker
{
    private static readonly List<TrackedElement> TrackedElements = [];
    private static readonly List<WeakReference<NavigationPage>> TrackedNavigationPages = [];

    public static async Task RunWhenDoneAsync(VisualElement visualElement, LifecycleAction action)
    {
        if (action.IsSuppressed(visualElement))
            return;

        if (visualElement is NavigationPage)
            return;

        var hostPage = Utilities.GetFirstSelfOrParentOfType<Page>(visualElement);
        if (hostPage is null)
        {
            action.OnDone(visualElement);
            return;
        }

        var navigationPage = Utilities.GetFirstSelfOrParentOfType<NavigationPage>(hostPage);
        if (navigationPage is not null)
        {
            TrackNavigationScope(visualElement, hostPage, navigationPage, action);
            return;
        }

        await Task.Delay(100);

        if (IsHostPageStillInNavigationStack(hostPage) ||
            IsHostPageStillHostedByShell(hostPage))
            return;

        var tab = Utilities.GetFirstSelfOrParentOfType<Tab>(hostPage);
        if (tab is null)
            action.OnDone(visualElement);
    }

    private static bool IsHostPageStillInNavigationStack(Page hostPage)
    {
        INavigation navigation = hostPage.Navigation;

        return navigation.NavigationStack.Contains(hostPage) ||
               navigation.ModalStack.Contains(hostPage);
    }

    private static bool IsHostPageStillHostedByShell(Page hostPage)
    {
        return Utilities.GetFirstSelfOrParentOfType<Shell>(hostPage) is not null;
    }

    private static void TrackNavigationScope(
        VisualElement visualElement,
        Page hostPage,
        NavigationPage navigationPage,
        LifecycleAction action)
    {
        if (navigationPage.Navigation.ModalStack.Count > 0)
            return;

        if (!navigationPage.IsLoaded)
        {
            if (!action.IsSuppressed(navigationPage))
                action.OnDone(visualElement);

            return;
        }

        CleanupDeadTrackedElements();

        foreach (TrackedElement trackedElement in TrackedElements)
        {
            if (trackedElement.Key == action.Key &&
                trackedElement.VisualElement.TryGetTarget(out VisualElement? target) &&
                target == visualElement)
                return;
        }

        TrackedElements.Add(new TrackedElement(
            action.Key,
            new WeakReference<VisualElement>(visualElement),
            new WeakReference<Page>(hostPage),
            action.IsSuppressed,
            action.OnDone));

        TrackNavigationPage(navigationPage);
    }

    private static void TrackNavigationPage(NavigationPage navigationPage)
    {
        foreach (WeakReference<NavigationPage> navPageReference in TrackedNavigationPages.ToArray())
        {
            if (!navPageReference.TryGetTarget(out NavigationPage? target))
            {
                TrackedNavigationPages.Remove(navPageReference);
                continue;
            }

            if (target == navigationPage)
                return;
        }

        navigationPage.Popped += OnNavigationPagePopped;
        TrackedNavigationPages.Add(new WeakReference<NavigationPage>(navigationPage));
    }

    private static void OnNavigationPagePopped(object? sender, NavigationEventArgs e)
    {
        if (sender is not NavigationPage navigationPage || e.Page is null)
            return;

        foreach (TrackedElement trackedElement in TrackedElements.ToArray())
        {
            if (!trackedElement.VisualElement.TryGetTarget(out VisualElement? visualElement) ||
                !trackedElement.HostPage.TryGetTarget(out Page? page))
            {
                TrackedElements.Remove(trackedElement);
                continue;
            }

            if (page != e.Page)
                continue;

            TrackedElements.Remove(trackedElement);

            if (!trackedElement.IsSuppressed(navigationPage))
                trackedElement.OnDone(visualElement);
        }
    }

    private static void CleanupDeadTrackedElements()
    {
        foreach (TrackedElement trackedElement in TrackedElements.ToArray())
            if (!trackedElement.VisualElement.TryGetTarget(out _) ||
                !trackedElement.HostPage.TryGetTarget(out _))
                TrackedElements.Remove(trackedElement);
    }

    private sealed record TrackedElement(
        string Key,
        WeakReference<VisualElement> VisualElement,
        WeakReference<Page> HostPage,
        Func<BindableObject, bool> IsSuppressed,
        Action<VisualElement> OnDone);
}
