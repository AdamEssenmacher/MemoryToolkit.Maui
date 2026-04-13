using Microsoft.Maui;

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
        vte.TearDown(MemoryToolkitConfiguration.Options.DefaultTearDownStrategy);
    }

    public static void TearDown(this IVisualTreeElement vte, TearDownStrategy strategy)
    {
        if (strategy == TearDownStrategy.DetectOnly)
            return;

        TearDownImpl(vte, true, strategy);

        return;

        void TearDownImpl(IVisualTreeElement current, bool isRoot, TearDownStrategy strategy)
        {
            if (current is not BindableObject bindableObject)
                return;

            // Suppress is self-explanatory. Cascade means it's already set for tear down, so no reason to double up.
            if (TearDownBehavior.GetSuppress(bindableObject) ||
                (!isRoot && TearDownBehavior.GetCascade(bindableObject)))
                return;

            if (strategy == TearDownStrategy.DisconnectHandlers)
            {
                if (current is IView view)
                    view.DisconnectHandlers();

                return;
            }

            foreach (IVisualTreeElement childElement in current.GetVisualChildren())
                TearDownImpl(childElement, false, strategy);

            ClearMauiReferences(current);

            if (current is VisualElement visualElement)
            {
                if (visualElement.Handler != null)
                {
                    TearDownBehavior.OnTearDown?.Invoke(visualElement);
                    visualElement.Handler.DisconnectHandler();
                }

                visualElement.Resources = null;
            }
            else if (current is Element element)
            {
                if (element.Handler != null)
                {
                    TearDownBehavior.OnTearDown?.Invoke(element);
                    element.Handler.DisconnectHandler();
                }
            }
        }
    }

    private static void ClearMauiReferences(IVisualTreeElement vte)
    {
        if (vte is Element element)
        {
            element.BindingContext = null;
            element.Parent = null;
            element.ClearLogicalChildren();
        }

#pragma warning disable CS0618
        if (vte is ListView listView)
            listView.ItemsSource = null;
#pragma warning restore CS0618
        else if (vte is ContentView contentView)
            contentView.Content = null;
        else if (vte is Border border)
            border.Content = null;
        else if (vte is ContentPage contentPage)
            contentPage.Content = null;
        else if (vte is ScrollView scrollView)
            scrollView.Content = null;
    }
}
