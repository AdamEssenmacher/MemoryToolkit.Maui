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

                visualElement.Resources = null;
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
        BuildFlatList(view, views);

        foreach (IView viewToDisconnect in views)
            DisconnectHandlerSafely(viewToDisconnect);

        return;

        static void BuildFlatList(IView current, List<IView> views)
        {
            if (current is BindableObject bindableObject &&
                HandlerProperties.GetDisconnectPolicy(bindableObject) == HandlerDisconnectPolicy.Manual)
                return;

            views.Add(current);

            if (current is not IVisualTreeElement visualTreeElement)
                return;

            foreach (IVisualTreeElement child in visualTreeElement.GetVisualChildren())
                if (child is IView childView)
                    BuildFlatList(childView, views);
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
            visualElement.Behaviors.Clear();

        if (vte is Element element)
        {
            element.BindingContext = null;
            element.Parent = null;
            element.ClearLogicalChildren();
        }

        if (vte is View view)
            view.GestureRecognizers.Clear();

        if (vte is Label label)
            ClearFormattedTextReferences(label);

        if (vte is ItemsView itemsView)
        {
            itemsView.ItemsSource = null;
            itemsView.ItemTemplate = null;
        }
#pragma warning disable CS0618
        else if (vte is ListView listView)
        {
            listView.ItemsSource = null;
            listView.ItemTemplate = null;
        }
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
