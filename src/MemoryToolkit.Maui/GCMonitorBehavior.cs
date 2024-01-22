namespace MemoryToolkit.Maui;

public class GCMonitorBehavior
{
    public static readonly BindableProperty CascadeProperty =
        BindableProperty.CreateAttached("Cascade", typeof(bool), typeof(GCMonitorBehavior), false,
            propertyChanged: CascadeChanged);

    public static readonly BindableProperty SuppressProperty =
        BindableProperty.CreateAttached("Suppress", typeof(bool), typeof(GCMonitorBehavior), false);

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

        Monitor(visualTreeElement);

        return;

        void Monitor(IVisualTreeElement vte)
        {
            if (vte is not BindableObject bindableObject)
                return;

            if (GetSuppress(bindableObject))
                return;

            foreach (IVisualTreeElement childElement in vte.GetVisualChildren())
                Monitor(childElement);

            GCCollectionMonitor.Instance.Monitor(vte);

            if (vte is VisualElement { Handler: not null } visualElement)
                GCCollectionMonitor.Instance.Monitor(visualElement.Handler);
            else if (vte is Element { Handler: not null } element)
                GCCollectionMonitor.Instance.Monitor(element.Handler);
        }
    }
}