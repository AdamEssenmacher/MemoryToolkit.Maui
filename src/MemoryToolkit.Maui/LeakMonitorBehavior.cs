// ReSharper disable MemberCanBePrivate.Global

namespace MemoryToolkit.Maui;

public class LeakMonitorBehavior
{
    public static readonly BindableProperty CascadeProperty =
        BindableProperty.CreateAttached("Cascade", typeof(bool), typeof(LeakMonitorBehavior), false,
            propertyChanged: CascadeChanged);

    private static readonly LifecycleAction MonitorWhenDone = new(
        nameof(LeakMonitorBehavior),
        GetSuppress,
        visualElement => visualElement.Monitor());

    private static void CascadeChanged(BindableObject view, object oldValue, object newValue)
    {
        if (view is not VisualElement visualElement)
            throw new InvalidOperationException(
                $"{nameof(LeakMonitorBehavior)}.Cascade can only be attached to a VisualElement");

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

        await ElementLifecycleTracker.RunWhenDoneAsync(visualElement, MonitorWhenDone);
    }

    #region Bindable Properties

    public static bool GetCascade(BindableObject view)
    {
        return (bool)view.GetValue(CascadeProperty);
    }

    public static void SetCascade(BindableObject view, bool value)
    {
        view.SetValue(CascadeProperty, value);
    }

    public static readonly BindableProperty SuppressProperty =
        BindableProperty.CreateAttached("Suppress", typeof(bool), typeof(LeakMonitorBehavior), false);

    public static bool GetSuppress(BindableObject view)
    {
        return (bool)view.GetValue(SuppressProperty);
    }

    public static void SetSuppress(BindableObject view, bool value)
    {
        view.SetValue(SuppressProperty, value);
    }

    public static readonly BindableProperty NameProperty =
        BindableProperty.CreateAttached("Name", typeof(string), typeof(LeakMonitorBehavior), null);

    public static string GetName(BindableObject view)
    {
        return (string)view.GetValue(NameProperty);
    }

    public static void SetName(BindableObject view, string value)
    {
        view.SetValue(NameProperty, value);
    }

    #endregion
}
