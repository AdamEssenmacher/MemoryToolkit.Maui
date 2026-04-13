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

        page.TearDown(TearDownStrategy.DisconnectHandlers);

        Assert.Same(root, page.Content);
        Assert.Same(bindingContext, page.BindingContext);
        Assert.Same(bindingContext, root.BindingContext);
        Assert.Same(bindingContext, label.BindingContext);
    }

    [Theory]
    [InlineData(TearDownStrategy.Compartmentalize)]
    [InlineData(TearDownStrategy.AggressiveLegacy)]
    public void GraphClearingStrategiesCompartmentalizeManagedReferences(TearDownStrategy strategy)
    {
        (ContentPage page, Grid root, Label label, _) = CreatePageGraph();

        page.TearDown(strategy);

        Assert.Null(page.Content);
        Assert.Null(page.BindingContext);
        Assert.Null(root.BindingContext);
        Assert.Null(label.BindingContext);
        Assert.Null(root.Parent);
        Assert.Null(label.Parent);
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
}
