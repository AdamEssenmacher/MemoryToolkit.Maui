namespace MemoryToolkit.Maui;

public static class Utilities
{
    public static T? GetFirstSelfOrParentOfType<T>(Element element) where T : class
    {
        if (element is T selfResult)
            return selfResult;

        Element? parent = element.Parent;
        while (parent is not null)
        {
            if (parent is T parentResult)
                return parentResult;

            parent = parent.Parent;
        }

        return null;
    }

    public static void Monitor(this object visualTreeElement)
    {
        List<CollectionTarget> collectionTargets = [];

        MonitorImpl(visualTreeElement, true);

        GarbageCollectionMonitor.Instance.MonitorAndForceCollectionAsync(collectionTargets);

        return;

        void MonitorImpl(object monitorTarget, bool isRoot)
        {
            if (monitorTarget is IVisualTreeElement vte)
            {
                string? targetName = null;
                if (vte is BindableObject bindableObject)
                {
                    // Suppress is self-explanatory. Cascade means it's already monitored, so no reason to double up.
                    if (LeakMonitorBehavior.GetSuppress(bindableObject) ||
                        (!isRoot && LeakMonitorBehavior.GetCascade(bindableObject)))
                        return;
                    
                    targetName = LeakMonitorBehavior.GetName(bindableObject);
                }

                foreach (IVisualTreeElement childElement in vte.GetVisualChildren())
                    MonitorImpl(childElement, false);

                collectionTargets.Add(new CollectionTarget(vte, targetName));

                if (vte is VisualElement { Handler: not null } visualElement)
                    collectionTargets.Add(new CollectionTarget(visualElement.Handler));
                else if (vte is Element { Handler: not null } element)
                    collectionTargets.Add(new CollectionTarget(element.Handler));
            }
            else
            {
                collectionTargets.Add(new CollectionTarget(monitorTarget));
            }
        }
    }

    public static void TearDown(this IVisualTreeElement vte)
    {
        TearDownImpl(vte, true);

        return;

        void TearDownImpl(IVisualTreeElement vte, bool isRoot)
        {
            if (vte is not BindableObject bindableObject)
                return;

            // Suppress is self-explanatory. Cascade means it's already set for tear down, so no reason to double up.
            if (TearDownBehavior.GetSuppress(bindableObject) ||
                (!isRoot && TearDownBehavior.GetCascade(bindableObject)))
                return;

            foreach (IVisualTreeElement childElement in vte.GetVisualChildren())
                TearDownImpl(childElement, false);

            if (vte is VisualElement visualElement)
            {
                // First, clear the BindingContext
                visualElement.BindingContext = null;
                
                // Next, isolate the element.
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

                visualElement.ClearLogicalChildren();

                // With the binding context cleared, and the element isolated, it has a chance to revert itself
                // to a 'default' state.

                // The _last_ thing we want to do is disconnect the handler.
                if (visualElement.Handler != null)
                {
                    TearDownBehavior.OnTearDown?.Invoke(visualElement);
                    if (visualElement.Handler is IDisposable disposableHandler)
                        disposableHandler.Dispose();
                    visualElement.Handler?.DisconnectHandler();
                }

                visualElement.Resources = null;
            }
            else if (vte is Element element)
            {
                element.BindingContext = null;
                
                element.Parent = null;

                element.ClearLogicalChildren();

                if (element.Handler != null)
                {
                    TearDownBehavior.OnTearDown?.Invoke(element);

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
}