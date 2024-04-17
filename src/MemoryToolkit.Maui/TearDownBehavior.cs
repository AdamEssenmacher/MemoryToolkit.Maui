// ReSharper disable MemberCanBePrivate.Global

namespace MemoryToolkit.Maui;

public static class TearDownBehavior
{
    #region Bindable Properties

    public static readonly BindableProperty CascadeProperty =
        BindableProperty.CreateAttached("Cascade", typeof(bool), typeof(TearDownBehavior), false,
            propertyChanged: CascadeChanged);

    public static bool GetCascade(BindableObject view)
    {
        return (bool)view.GetValue(CascadeProperty);
    }

    public static void SetCascade(BindableObject view, bool value)
    {
        view.SetValue(CascadeProperty, value);
    }

    public static readonly BindableProperty SuppressProperty =
        BindableProperty.CreateAttached("Suppress", typeof(bool), typeof(TearDownBehavior), false);

    public static bool GetSuppress(BindableObject view)
    {
        return (bool)view.GetValue(SuppressProperty);
    }

    public static void SetSuppress(BindableObject view, bool value)
    {
        view.SetValue(SuppressProperty, value);
    }

    #endregion

    // We need to keep hold of our monitored elements that were in a navigation page until the page is popped.
    private static readonly List<Tuple<WeakReference<VisualElement>, WeakReference<Page>>> TrackedElements = [];

    // We also need to keep hold of navigation pages that we're subscribed to.
    private static readonly List<WeakReference<NavigationPage>> TrackedNavigationPages = [];
    
    public static Action<object>? OnTearDown { get; set; }

    private static void CascadeChanged(BindableObject view, object oldValue, object newValue)
    {
        if (view is not VisualElement visualElement)
            throw new InvalidOperationException(
                $"{nameof(TearDownBehavior)}.Cascade can only be attached to a VisualElement");

        var attachBehavior = (bool)newValue;
        if (attachBehavior)
            visualElement.Unloaded += OnVisualElementUnloaded;
        else
            visualElement.Unloaded -= OnVisualElementUnloaded;
    }

    private static async void OnVisualElementUnloaded(object? sender, EventArgs e)
    {
        if (sender is not VisualElement visualElement)
            return;

        if (GetSuppress(visualElement))
            return;

        // If the VisualElement is ultimately hosted in a Navigation page, we only want to monitor for
        // garbage collection if the element's page is no longer in the navigation stack
        // OR if the navigation page itself gets unloaded.

        // Find the first parent element (or self) that is a Page. This is the containing or 'host' page.
        var hostPage = Utilities.GetFirstSelfOrParentOfType<Page>(visualElement);

        // If the host page is null, we can Disconnect immediately since there's no navigation context.
        if (hostPage is null)
        {
            visualElement.TearDown();
            return;
        }

        // Since we are in a host page, we next need to determine if we're in a NavigationPage, in Shell, or neither.
        // These should be exclusive.
        var navigationPage = Utilities.GetFirstSelfOrParentOfType<NavigationPage>(hostPage);
        if (navigationPage != null)
        {
            // If we're in a NavigationPage, and the NavigationPage is not loaded, then we can TearDown immediately.
            if (!navigationPage.IsLoaded)
            {
                // ...unless Suppress is set.
                if (GetSuppress(navigationPage))
                    return;

                visualElement.TearDown();
                return;
            }

            // If we make it to this point, then we're in the scope of a navigation page that is still loaded.
            // This means that the Unloaded event could be firing because the page is being popped, or pushed over.
            // We don't know which it is yet.
            // As a page is being popped, it will still be in the navigation stack,
            // so we need to wait until the Popped event is fired before we can check.
            // The following approach temporarily keeps track of the visual element and its host page.
            // We then subscribe to the Popped event of the navigation page and wait for it to fire.

            // Don't monitor the same object more than once. This could happen, for example,
            // if the tracked element is in a page that has been pushed over multiple times.
            foreach (Tuple<WeakReference<VisualElement>, WeakReference<Page>> tuple in TrackedElements.ToArray())
                if (tuple.Item1.TryGetTarget(out VisualElement? target) && target == visualElement)
                    return;

            TrackedElements.Add(new Tuple<WeakReference<VisualElement>, WeakReference<Page>>(
                new WeakReference<VisualElement>(visualElement), new WeakReference<Page>(hostPage)));

            // Don't subscribe to the same navigation page more than once.
            foreach (WeakReference<NavigationPage> navPageReference in TrackedNavigationPages.ToArray())
                if (!navPageReference.TryGetTarget(out NavigationPage? target))
                    TrackedNavigationPages.Remove(navPageReference);
                else if (target == navigationPage)
                    return;

            // This shouldn't cause memory leaks since the handlers are static.
            navigationPage.Popped += OnNavigationPagePopped;
            TrackedNavigationPages.Add(new WeakReference<NavigationPage>(navigationPage));

            return;
        }

        // I don't know if there's a cleaner way to do this in Shell.
        // If we're being popped, then the page will no longer have a Tab parent after a short delay
        await Task.Delay(100);
        var tab = Utilities.GetFirstSelfOrParentOfType<Tab>(hostPage);
        if (tab == null)
            visualElement.TearDown();
    }

    private static void OnNavigationPagePopped(object? sender, NavigationEventArgs e)
    {
        if (sender is not NavigationPage navigationPage)
            return;

        if (GetSuppress(navigationPage))
            return;

        Page? poppedPage = e.Page;
        if (poppedPage == null)
            return;

        foreach (Tuple<WeakReference<VisualElement>, WeakReference<Page>> trackedElement in TrackedElements.ToArray())
        {
            if (!trackedElement.Item1.TryGetTarget(out VisualElement? visualElement) ||
                !trackedElement.Item2.TryGetTarget(out Page? page))
            {
                TrackedElements.Remove(trackedElement);
                continue;
            }

            if (page != poppedPage)
                continue;

            TrackedElements.Remove(trackedElement);
            visualElement.TearDown();
        }
    }
}