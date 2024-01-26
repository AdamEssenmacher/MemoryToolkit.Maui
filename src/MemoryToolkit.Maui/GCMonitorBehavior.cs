namespace MemoryToolkit.Maui;

public class GCMonitorBehavior
{
    public static readonly BindableProperty CascadeProperty =
        BindableProperty.CreateAttached("Cascade", typeof(bool), typeof(GCMonitorBehavior), false,
            propertyChanged: CascadeChanged);

    public static readonly BindableProperty SuppressProperty =
        BindableProperty.CreateAttached("Suppress", typeof(bool), typeof(GCMonitorBehavior), false);

    public static readonly BindableProperty NameProperty =
        BindableProperty.CreateAttached("Name", typeof(string), typeof(GCMonitorBehavior), null);

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
    
    public static string GetName(BindableObject view)
    {
        return (string)view.GetValue(NameProperty);
    }

    public static void SetSuppress(BindableObject view, string value)
    {
        view.SetValue(NameProperty, value);
    }

    private static void CascadeChanged(BindableObject view, object oldValue, object newValue)
    {
        if (view is not VisualElement visualElement)
            throw new InvalidOperationException(
                "GCMonitorBehavior.Cascade can only be attached to a VisualElement");

        var attachBehavior = (bool)newValue;
        if (attachBehavior)
            visualElement.Unloaded += OnVisualElementUnloaded;
        else
            visualElement.Unloaded -= OnVisualElementUnloaded;
    }

    private static void OnVisualElementUnloaded(object? sender, EventArgs e)
    {
        if (sender is not VisualElement senderElement)
            return;

        if (GetSuppress(senderElement))
            return;

        var visualTreeElement = (IVisualTreeElement)senderElement;
        
        List<GCCollectionItem> targets = new();

        Monitor(visualTreeElement, true);

        GCCollectionMonitor.Instance.MonitorAndForceCollectionAsync(targets);

        return;

        void Monitor(IVisualTreeElement vte, bool isRoot)
        {
            if (vte is not BindableObject bindableObject)
                return;

            // Suppress is self-explanatory. Cascade means it's already monitored, so no reason to double up.
            if (GetSuppress(bindableObject) || (!isRoot && GetCascade(bindableObject)))
                return;

            foreach (IVisualTreeElement childElement in vte.GetVisualChildren())
                Monitor(childElement, false);

            Action<GCCollectionItem>? onLeaked = null;
            Action<GCCollectionItem>? onCollected = null;
            if (Application.Current is GCMonitoredApplication gcMonitoredApplication)
            {
                onLeaked = gcMonitoredApplication.OnLeaked;
                onCollected = gcMonitoredApplication.OnCollected;
            }

            targets.Add(new GCCollectionItem(vte, GetName(bindableObject), onLeaked, onCollected));

            if (vte is VisualElement { Handler: not null } visualElement)
                targets.Add(new GCCollectionItem(visualElement.Handler, null, onLeaked, onCollected));
            else if (vte is Element { Handler: not null } element)
                targets.Add(new GCCollectionItem(element.Handler, null, onLeaked, onCollected));
        }
    }
}