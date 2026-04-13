using Microsoft.Extensions.Logging;
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
                    DisconnectHandlersSafely(view);

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
                    DisconnectHandlerSafely(visualElement);
                }

                ClearMauiReference(current, "resources", () => visualElement.Resources = null);
            }
            else if (current is Element element)
            {
                if (element.Handler != null)
                {
                    TearDownBehavior.OnTearDown?.Invoke(element);
                    DisconnectHandlerSafely(element);
                }
            }
        }
    }

    private static void DisconnectHandlersSafely(IView view)
    {
        List<IView> views = [];
        BuildFlatList(view, views, true);

        foreach (IView viewToDisconnect in views)
            DisconnectHandlerSafely(viewToDisconnect);

        return;

        static void BuildFlatList(IView current, List<IView> views, bool isRoot)
        {
            if (current is BindableObject bindableObject)
            {
                if (HandlerProperties.GetDisconnectPolicy(bindableObject) == HandlerDisconnectPolicy.Manual ||
                    TearDownBehavior.GetSuppress(bindableObject) ||
                    (!isRoot && TearDownBehavior.GetCascade(bindableObject)))
                    return;
            }

            views.Add(current);

            if (current is not IVisualTreeElement visualTreeElement)
                return;

            foreach (IVisualTreeElement child in visualTreeElement.GetVisualChildren())
                if (child is IView childView)
                    BuildFlatList(childView, views, false);
        }
    }

    private static void DisconnectHandlerSafely(IElement element)
    {
        IElementHandler? handler = element.Handler;
        if (handler == null)
            return;

        try
        {
            handler.DisconnectHandler();
        }
        catch (Exception exception)
        {
            GarbageCollectionMonitor.Instance.Logger.LogWarning(
                exception,
                "Exception while disconnecting handler for {ElementType}",
                element.GetType().FullName);
        }
    }

    private static void ClearMauiReferences(IVisualTreeElement vte)
    {
        if (vte is VisualElement visualElement)
            ClearMauiReference(vte, "behaviors", () => visualElement.Behaviors.Clear());

        if (vte is Element element)
        {
            ClearMauiReference(vte, "binding context", () => element.BindingContext = null);
            ClearMauiReference(vte, "parent", () => element.Parent = null);
            ClearMauiReference(vte, "logical children", element.ClearLogicalChildren);
        }

        if (vte is View view)
            ClearMauiReference(vte, "gesture recognizers", () => view.GestureRecognizers.Clear());

        if (vte is Label label)
            ClearMauiReference(vte, "formatted text", () => ClearFormattedTextReferences(label));

        if (vte is ItemsView itemsView)
        {
            ClearMauiReference(vte, "item source", () => itemsView.ItemsSource = null);
            ClearMauiReference(vte, "item template", () => itemsView.ItemTemplate = null);
        }
#pragma warning disable CS0618
        else if (vte is ListView listView)
        {
            ClearMauiReference(vte, "item source", () => listView.ItemsSource = null);
            ClearMauiReference(vte, "item template", () => listView.ItemTemplate = null);
        }
#pragma warning restore CS0618
        else if (vte is ContentView contentView)
            ClearMauiReference(vte, "content", () => contentView.Content = null);
        else if (vte is Border border)
            ClearMauiReference(vte, "content", () => border.Content = null);
        else if (vte is ContentPage contentPage)
            ClearMauiReference(vte, "content", () => contentPage.Content = null);
        else if (vte is ScrollView scrollView)
            ClearMauiReference(vte, "content", () => scrollView.Content = null);
    }

    private static void ClearMauiReference(IVisualTreeElement vte, string referenceName, Action clearReference)
    {
        try
        {
            clearReference();
        }
        catch (Exception exception)
        {
            GarbageCollectionMonitor.Instance.Logger.LogWarning(
                exception,
                "Exception while clearing {ReferenceName} for {ElementType}",
                referenceName,
                vte.GetType().FullName);
        }
    }

    private static void ClearFormattedTextReferences(Label label)
    {
        if (label.FormattedText is not { } formattedText)
            return;

        foreach (Span span in formattedText.Spans)
            span.GestureRecognizers.Clear();

        formattedText.Spans.Clear();
        label.FormattedText = null;
    }
}
