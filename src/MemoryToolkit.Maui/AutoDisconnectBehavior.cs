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

        Disconnect(visualTreeElement, true);

        return;

        void Disconnect(IVisualTreeElement vte, bool isRoot)
        {
            if (vte is not BindableObject bindableObject)
                return;

            if (GetSuppress(bindableObject) || (!isRoot && GetCascade(bindableObject)))
                return;

            foreach (IVisualTreeElement childElement in vte.GetVisualChildren())
                Disconnect(childElement, false);
            
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
                    if(visualElement.Handler is IDisposable disposableHandler)
                        disposableHandler.Dispose();
                    visualElement.Handler.DisconnectHandler();
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
                    if(element.Handler.PlatformView is IDisposable disposablePlatformView)
                        disposablePlatformView.Dispose();
                    if(element.Handler is IDisposable disposableElementHandler)
                        disposableElementHandler.Dispose();
                    element.Handler.DisconnectHandler();
                }
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