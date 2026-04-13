// ReSharper disable MemberCanBePrivate.Global

namespace MemoryToolkit.Maui;

public static class TearDownBehavior
{
    public static readonly BindableProperty CascadeProperty =
        BindableProperty.CreateAttached("Cascade", typeof(bool), typeof(TearDownBehavior), false,
            propertyChanged: CascadeChanged);

    private static readonly LifecycleAction TearDownWhenDone = new(
        nameof(TearDownBehavior),
        GetSuppress,
        visualElement => visualElement.TearDown(GetEffectiveStrategy(visualElement)));

    public static Action<object>? OnTearDown { get; set; }

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

        await ElementLifecycleTracker.RunWhenDoneAsync(visualElement, TearDownWhenDone);
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
        BindableProperty.CreateAttached("Suppress", typeof(bool), typeof(TearDownBehavior), false);

    public static bool GetSuppress(BindableObject view)
    {
        return (bool)view.GetValue(SuppressProperty);
    }

    public static void SetSuppress(BindableObject view, bool value)
    {
        view.SetValue(SuppressProperty, value);
    }

    public static readonly BindableProperty StrategyProperty =
        BindableProperty.CreateAttached("Strategy", typeof(TearDownStrategy?), typeof(TearDownBehavior), null);

    public static TearDownStrategy? GetStrategy(BindableObject view)
    {
        return (TearDownStrategy?)view.GetValue(StrategyProperty);
    }

    public static void SetStrategy(BindableObject view, TearDownStrategy? value)
    {
        view.SetValue(StrategyProperty, value);
    }

    private static TearDownStrategy GetEffectiveStrategy(BindableObject view)
    {
        return GetStrategy(view) ?? MemoryToolkitConfiguration.Options.DefaultTearDownStrategy;
    }

    #endregion
}
