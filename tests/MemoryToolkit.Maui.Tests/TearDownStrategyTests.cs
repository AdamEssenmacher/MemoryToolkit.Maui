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
        var itemsSource = new[] { new object() };
        var itemTemplate = new DataTemplate(() => new Label());
        var collectionView = new CollectionView
        {
            ItemsSource = itemsSource,
            ItemTemplate = itemTemplate
        };
        (FormattedString formattedText, Span span, TapGestureRecognizer spanGestureRecognizer) =
            AddFormattedTextWithGesture(label);
        root.Add(collectionView);
        label.Behaviors.Add(behavior);
        label.GestureRecognizers.Add(gestureRecognizer);

        page.TearDown(TearDownStrategy.DisconnectHandlers);

        Assert.Same(root, page.Content);
        Assert.Same(bindingContext, page.BindingContext);
        Assert.Same(bindingContext, root.BindingContext);
        Assert.Same(bindingContext, label.BindingContext);
        Assert.Same(itemsSource, collectionView.ItemsSource);
        Assert.Same(itemTemplate, collectionView.ItemTemplate);
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
        var collectionView = new CollectionView
        {
            ItemsSource = new[] { new object() },
            ItemTemplate = new DataTemplate(() => new Label())
        };
        (FormattedString formattedText, Span span, _) = AddFormattedTextWithGesture(label);
        root.Add(collectionView);
        label.Behaviors.Add(new TestBehavior());
        label.GestureRecognizers.Add(new TapGestureRecognizer());

        page.TearDown(TearDownStrategy.Compartmentalize);

        Assert.Null(page.Content);
        Assert.Null(page.BindingContext);
        Assert.Null(root.BindingContext);
        Assert.Null(label.BindingContext);
        Assert.Null(root.Parent);
        Assert.Null(label.Parent);
        Assert.Null(collectionView.ItemsSource);
        Assert.Null(collectionView.ItemTemplate);
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
    public void TearDownContinuesWhenAHandlerDisconnectThrowsObjectDisposedException()
    {
        var throwingLabel = new Label();
        var healthyLabel = new Label();
        var throwingHandler = new TestElementHandler
        {
            ThrowOnDisconnect = true,
            DisconnectException = new ObjectDisposedException("Disposed handler")
        };
        var healthyHandler = new TestElementHandler();
        throwingLabel.Handler = throwingHandler;
        healthyLabel.Handler = healthyHandler;

        var root = new Grid();
        root.Add(throwingLabel);
        root.Add(healthyLabel);

        Exception? exception = Record.Exception(() => root.TearDown(TearDownStrategy.DisconnectHandlers));

        Assert.Null(exception);
        Assert.Equal(1, throwingHandler.DisconnectCalls);
        Assert.Equal(1, healthyHandler.DisconnectCalls);
    }

    [Theory]
    [InlineData(TearDownStrategy.DetectOnly)]
    [InlineData(TearDownStrategy.DisconnectHandlers)]
    public void NonCompartmentalizeStrategiesDoNotInvokeTearDownHook(TearDownStrategy strategy)
    {
        var label = new Label
        {
            Handler = new TestElementHandler()
        };
        var tearDownTargets = new List<object>();
        TearDownBehavior.OnTearDown = tearDownTargets.Add;

        try
        {
            label.TearDown(strategy);
        }
        finally
        {
            TearDownBehavior.OnTearDown = null;
        }

        Assert.Empty(tearDownTargets);
    }

    [Fact]
    public void CompartmentalizeInvokesTearDownHookForHandledElements()
    {
        var label = new Label();
        var handler = new TestElementHandler();
        label.Handler = handler;
        var tearDownTargets = new List<object>();
        TearDownBehavior.OnTearDown = tearDownTargets.Add;

        try
        {
            label.TearDown(TearDownStrategy.Compartmentalize);
        }
        finally
        {
            TearDownBehavior.OnTearDown = null;
        }

        Assert.Same(label, Assert.Single(tearDownTargets));
        Assert.Equal(1, handler.DisconnectCalls);
    }

    [Fact]
    public void CompartmentalizeContinuesWhenManagedReferenceClearingThrows()
    {
        var handler = new TestElementHandler();
        var contentView = new ThrowingContentView
        {
            Content = new Label(),
            Handler = handler,
            ThrowWhenClearingContent = true
        };

        Exception? exception = Record.Exception(() => contentView.TearDown(TearDownStrategy.Compartmentalize));

        Assert.Null(exception);
        Assert.Equal(1, handler.DisconnectCalls);
        Assert.NotNull(contentView.Content);
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

    private sealed class ThrowingContentView : ContentView
    {
        public bool ThrowWhenClearingContent { get; init; }

        protected override void OnPropertyChanging(string? propertyName = null)
        {
            if (ThrowWhenClearingContent && propertyName == nameof(Content) && Content is not null)
                throw new NullReferenceException("Simulated content clear failure.");

            base.OnPropertyChanging(propertyName);
        }
    }

    private sealed class TestElementHandler : IViewHandler
    {
        public bool ThrowOnDisconnect { get; init; }

        public Exception? DisconnectException { get; init; }

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
                throw DisconnectException ?? new InvalidOperationException("Test disconnect failure.");
        }
    }
}
