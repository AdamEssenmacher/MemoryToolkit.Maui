// ReSharper disable MemberCanBePrivate.Global

namespace MemoryToolkit.Maui;

public static class AutoDisconnectBehavior
{
    public static readonly BindableProperty CascadeProperty =
        BindableProperty.CreateAttached("Cascade", typeof(bool), typeof(AutoDisconnectBehavior), false,
            propertyChanged: CascadeChanged);

    public static readonly BindableProperty SuppressProperty =
        BindableProperty.CreateAttached("Suppress", typeof(bool), typeof(AutoDisconnectBehavior), false);

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
                "AutoDisconnectBehavior.Cascade can only be attached to a VisualElement");

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

        Disconnect(visualTreeElement);

        return;

        void Disconnect(IVisualTreeElement vte)
        {
            if (vte is not BindableObject bindableObject)
                return;

            if (GetSuppress(bindableObject))
                return;

            foreach (IVisualTreeElement childElement in vte.GetVisualChildren())
                Disconnect(childElement);

            if (vte is VisualElement visualElement)
            {
                OnDisconnectingHandler?.Invoke(null, new DisconnectingHandlerEventArgs(visualElement));
                visualElement.Handler?.DisconnectHandler();
                visualElement.Parent = null;
            }
            else if (vte is Element element)
            {
                OnDisconnectingHandler?.Invoke(null, new DisconnectingHandlerEventArgs(element));
                element.Handler?.DisconnectHandler();
                element.Parent = null;
            }
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