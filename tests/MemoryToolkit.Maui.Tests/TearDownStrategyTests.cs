namespace MemoryToolkit.Maui.Tests;

public sealed class TearDownStrategyTests
{
    [Fact]
    public void DetectOnlyLeavesObjectGraphIntact()
    {
        (ContentPage page, Grid root, Label label, object bindingContext) = CreatePageGraph();

        page.TearDown(TearDownStrategy.DetectOnly);

        Assert.Same(root, page.Content);
        Assert.Same(bindingContext, page.BindingContext);
        Assert.Same(bindingContext, root.BindingContext);
        Assert.Same(bindingContext, label.BindingContext);
    }

    [Fact]
    public void DisconnectHandlersDoesNotClearManagedObjectGraph()
    {
        (ContentPage page, Grid root, Label label, object bindingContext) = CreatePageGraph();
        var gestureRecognizer = new TapGestureRecognizer();
        var behavior = new TestBehavior();
        (FormattedString formattedText, Span span, TapGestureRecognizer spanGestureRecognizer) =
            AddFormattedTextWithGesture(label);
        label.Behaviors.Add(behavior);
        label.GestureRecognizers.Add(gestureRecognizer);

        page.TearDown(TearDownStrategy.DisconnectHandlers);

        Assert.Same(root, page.Content);
        Assert.Same(bindingContext, page.BindingContext);
        Assert.Same(bindingContext, root.BindingContext);
        Assert.Same(bindingContext, label.BindingContext);
        Assert.Same(behavior, Assert.Single(label.Behaviors));
        Assert.Same(gestureRecognizer, Assert.Single(label.GestureRecognizers));
        Assert.Same(formattedText, label.FormattedText);
        Assert.Contains(span, label.FormattedText.Spans);
        Assert.Same(spanGestureRecognizer, Assert.Single(span.GestureRecognizers));
    }

    [Fact]
    public void CompartmentalizeClearsManagedReferences()
    {
        (ContentPage page, Grid root, Label label, _) = CreatePageGraph();
        (FormattedString formattedText, Span span, _) = AddFormattedTextWithGesture(label);
        label.Behaviors.Add(new TestBehavior());
        label.GestureRecognizers.Add(new TapGestureRecognizer());

        page.TearDown(TearDownStrategy.Compartmentalize);

        Assert.Null(page.Content);
        Assert.Null(page.BindingContext);
        Assert.Null(root.BindingContext);
        Assert.Null(label.BindingContext);
        Assert.Null(root.Parent);
        Assert.Null(label.Parent);
        Assert.Empty(label.Behaviors);
        Assert.Empty(label.GestureRecognizers);
        Assert.Null(label.FormattedText);
        Assert.Empty(formattedText.Spans);
        Assert.Empty(span.GestureRecognizers);
    }

    [Theory]
    [InlineData(TearDownStrategy.DisconnectHandlers)]
    [InlineData(TearDownStrategy.Compartmentalize)]
    public void TearDownContinuesWhenAHandlerDisconnectThrows(TearDownStrategy strategy)
    {
        var throwingLabel = new Label();
        var healthyLabel = new Label();
        var throwingHandler = new TestElementHandler { ThrowOnDisconnect = true };
        var healthyHandler = new TestElementHandler();
        throwingLabel.Handler = throwingHandler;
        healthyLabel.Handler = healthyHandler;

        var root = new Grid();
        root.Add(throwingLabel);
        root.Add(healthyLabel);

        Exception? exception = Record.Exception(() => root.TearDown(strategy));

        Assert.Null(exception);
        Assert.Equal(1, throwingHandler.DisconnectCalls);
        Assert.Equal(1, healthyHandler.DisconnectCalls);
    }

    [Fact]
    public void DisconnectHandlersHonorsManualDisconnectPolicy()
    {
        var label = new Label();
        var handler = new TestElementHandler();
        label.Handler = handler;
        HandlerProperties.SetDisconnectPolicy(label, HandlerDisconnectPolicy.Manual);

        label.TearDown(TearDownStrategy.DisconnectHandlers);

        Assert.Equal(0, handler.DisconnectCalls);
    }

    private static (ContentPage Page, Grid Root, Label Label, object BindingContext) CreatePageGraph()
    {
        var bindingContext = new object();
        var label = new Label { Text = "Target" };
        var root = new Grid
        {
            BindingContext = bindingContext
        };
        root.Add(label);

        var page = new ContentPage
        {
            BindingContext = bindingContext,
            Content = root
        };

        label.BindingContext = bindingContext;

        return (page, root, label, bindingContext);
    }

    private static (FormattedString FormattedText, Span Span, TapGestureRecognizer GestureRecognizer)
        AddFormattedTextWithGesture(Label label)
    {
        var gestureRecognizer = new TapGestureRecognizer();
        var span = new Span { Text = "Link" };
        span.GestureRecognizers.Add(gestureRecognizer);

        var formattedText = new FormattedString();
        formattedText.Spans.Add(new Span { Text = "Text " });
        formattedText.Spans.Add(span);
        label.FormattedText = formattedText;

        return (formattedText, span, gestureRecognizer);
    }

    private sealed class TestBehavior : Behavior<Label>
    {
    }

    private sealed class TestElementHandler : IViewHandler
    {
        public bool ThrowOnDisconnect { get; init; }

        public int DisconnectCalls { get; private set; }

        public object? PlatformView => null;

        public object? ContainerView => null;

        public bool HasContainer { get; set; }

        public IView? VirtualView { get; private set; }

        IElement? IElementHandler.VirtualView => VirtualView;

        public IMauiContext? MauiContext { get; private set; }

        public void SetMauiContext(IMauiContext mauiContext)
        {
            MauiContext = mauiContext;
        }

        public void SetVirtualView(IElement view)
        {
            VirtualView = (IView)view;
        }

        public void UpdateValue(string property)
        {
        }

        public void Invoke(string command, object? args)
        {
        }

        public Microsoft.Maui.Graphics.Size GetDesiredSize(double widthConstraint, double heightConstraint)
        {
            return Microsoft.Maui.Graphics.Size.Zero;
        }

        public void PlatformArrange(Microsoft.Maui.Graphics.Rect frame)
        {
        }

        public void DisconnectHandler()
        {
            DisconnectCalls++;

            if (ThrowOnDisconnect)
                throw new InvalidOperationException("Test disconnect failure.");
        }
    }
}
