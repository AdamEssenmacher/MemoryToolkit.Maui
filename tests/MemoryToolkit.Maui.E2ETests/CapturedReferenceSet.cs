namespace MemoryToolkit.Maui.E2ETests;

internal sealed record CapturedReferenceSet(
    IReadOnlyList<WeakReference> Views,
    IReadOnlyList<WeakReference> Handlers,
    IReadOnlyList<WeakReference> PlatformViews)
{
    public static CapturedReferenceSet Capture(IVisualTreeElement root)
    {
        var views = new List<WeakReference>();
        var handlers = new List<WeakReference>();
        var platformViews = new List<WeakReference>();

        Capture(root, views, handlers, platformViews);

        return new CapturedReferenceSet(views, handlers, platformViews);
    }

    public int AliveViews => Views.Count(reference => reference.IsAlive);

    public int AliveHandlers => Handlers.Count(reference => reference.IsAlive);

    public int AlivePlatformViews => PlatformViews.Count(reference => reference.IsAlive);

    public Dictionary<string, string> ToObservations()
    {
        return new Dictionary<string, string>
        {
            ["viewsTracked"] = Views.Count.ToString(),
            ["viewsAlive"] = AliveViews.ToString(),
            ["handlersTracked"] = Handlers.Count.ToString(),
            ["handlersAlive"] = AliveHandlers.ToString(),
            ["platformViewsTracked"] = PlatformViews.Count.ToString(),
            ["platformViewsAlive"] = AlivePlatformViews.ToString()
        };
    }

    private static void Capture(
        IVisualTreeElement current,
        ICollection<WeakReference> views,
        ICollection<WeakReference> handlers,
        ICollection<WeakReference> platformViews)
    {
        views.Add(new WeakReference(current));

        if (current is Element { Handler: not null } element)
        {
            handlers.Add(new WeakReference(element.Handler));

            if (element.Handler.PlatformView is not null)
                platformViews.Add(new WeakReference(element.Handler.PlatformView));
        }

        foreach (IVisualTreeElement child in current.GetVisualChildren())
            Capture(child, views, handlers, platformViews);
    }
}
