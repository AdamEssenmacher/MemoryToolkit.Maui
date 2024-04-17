// ReSharper disable MemberCanBePrivate.Global

namespace MemoryToolkit.Maui;

public static class TearDownBehavior
{
    public static readonly BindableProperty CascadeProperty =
        BindableProperty.CreateAttached("Cascade", typeof(bool), typeof(TearDownBehavior), false,
            propertyChanged: CascadeChanged);

    public static readonly BindableProperty SuppressProperty =
        BindableProperty.CreateAttached("Suppress", typeof(bool), typeof(TearDownBehavior), false);

    // We need to keep hold of our monitored elements that were in a navigation page until the page is popped.
    private static readonly List<Tuple<WeakReference<VisualElement>, WeakReference<Page>>> TrackedElements = new();

    // We also need to keep hold of navigation pages that we're subscribed to.
    private static readonly List<WeakReference<NavigationPage>> TrackedNavigationPages = new();

#pragma warning disable CS0067 // Event is never used
    public static event EventHandler<DisconnectingHandlerEventArgs>? OnDisconnectingHandler;
#pragma warning restore CS0067 // Event is never used

    public static bool GetCascade(BindableObject view)
    {
        return (bool)view.GetValue(CascadeProperty);
    }

    public static void SetCascade(BindableObject view, bool value)
    {
        view.SetValue(CascadeProperty, value);
    }

    public static bool GetSuppress(BindableObject view)
    {
        return (bool)view.GetValue(SuppressProperty);
    }

    public static void SetSuppress(BindableObject view, bool value)
    {
        view.SetValue(SuppressProperty, value);
    }

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
            Disconnect(visualElement);
            return;
        }

        // Next, we need to determine if we're in a NavigationPage, or in Shell. These should be exclusive.
        var navigationPage = Utilities.GetFirstSelfOrParentOfType<NavigationPage>(hostPage);
        if (navigationPage != null)
        {
            if (!navigationPage.IsLoaded)
            {
                if (GetSuppress(navigationPage))
                    return;
                
                Disconnect(visualElement);
                return;
            }

            // If we make it to this point, then we're in the scope of a navigation page.
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

        // If we're being popped, then the page will no longer have a Tab parent after a short delay
        await Task.Delay(100);
        var tab = Utilities.GetFirstSelfOrParentOfType<Tab>(hostPage);
        if (tab == null)
            Disconnect(visualElement);
    }

    private static void Disconnect(IVisualTreeElement vte)
    {
        DisconnectImpl(vte, true);

        return;

        void DisconnectImpl(IVisualTreeElement vte, bool isRoot)
        {
            if (vte is not BindableObject bindableObject)
                return;

            if (GetSuppress(bindableObject) || (!isRoot && GetCascade(bindableObject)))
                return;

            foreach (IVisualTreeElement childElement in vte.GetVisualChildren())
                DisconnectImpl(childElement, false);

            if (vte is VisualElement visualElement)
            {
                // First, isolate the element. This will null out the binding context if it is inherited,
                visualElement.Parent = null;

                if (vte is ListView listView)
                    listView.ItemsSource = null;
                else if (vte is ContentView contentView)
                    contentView.Content = null;
                else if (vte is Border border)
                    border.Content = null;
                else if (vte is ContentPage contentPage)
                    contentPage.Content = null;
                else if (vte is ScrollView scrollView)
                    scrollView.Content = null;

                // Next, clear the BindingContext (if it is not inherited)
                visualElement.BindingContext = null;

                visualElement.ClearLogicalChildren();

                // With the binding context cleared, and the element isolated, it has a chance to revert itself
                // to a 'default' state.

                // The _last_ thing we want to do is disconnect the handler.
                if (visualElement.Handler != null)
                {
                    OnDisconnectingHandler?.Invoke(null, new DisconnectingHandlerEventArgs(visualElement));
                    if (visualElement.Handler is IDisposable disposableHandler)
                        disposableHandler.Dispose();
                    visualElement.Handler?.DisconnectHandler();
                }

                visualElement.Resources = null;
            }
            else if (vte is Element element)
            {
                element.Parent = null;

                element.BindingContext = null;

                element.ClearLogicalChildren();

                if (element.Handler != null)
                {
                    OnDisconnectingHandler?.Invoke(null, new DisconnectingHandlerEventArgs(element));

#if IOS
                    // Fixes issue specific to ListView on iOS, where RealCell is not nulled out.
                    if (element is ViewCell && element.Handler.PlatformView is IDisposable disposablePlatformView)
                        disposablePlatformView.Dispose();
#endif

                    if (element.Handler is IDisposable disposableElementHandler)
                        disposableElementHandler.Dispose();
                    element.Handler.DisconnectHandler();
                }
            }
        }
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
            Disconnect(visualElement);
        }
    }

    public class DisconnectingHandlerEventArgs : EventArgs
    {
        public DisconnectingHandlerEventArgs(object element)
        {
            Element = element;
        }

        public object Element { get; }
    }
}