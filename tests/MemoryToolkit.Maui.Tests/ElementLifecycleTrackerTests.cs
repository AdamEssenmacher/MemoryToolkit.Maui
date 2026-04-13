namespace MemoryToolkit.Maui.Tests;

public sealed class ElementLifecycleTrackerTests
{
    [Fact]
    public async Task RunWhenDoneRunsImmediatelyWhenElementHasNoHostPage()
    {
        var element = new Label();
        var wasCalled = false;
        var action = new LifecycleAction(
            "test",
            _ => false,
            visualElement => wasCalled = ReferenceEquals(visualElement, element));

        await ElementLifecycleTracker.RunWhenDoneAsync(element, action);

        Assert.True(wasCalled);
    }

    [Fact]
    public async Task RunWhenDoneHonorsSuppression()
    {
        var element = new Label();
        var wasCalled = false;
        var action = new LifecycleAction(
            "test",
            _ => true,
            _ => wasCalled = true);

        await ElementLifecycleTracker.RunWhenDoneAsync(element, action);

        Assert.False(wasCalled);
    }

    [Fact]
    public async Task RunWhenDoneRunsForNonNavigationPageAfterFallbackDelay()
    {
        var element = new Label();
        var page = new ContentPage
        {
            Content = element
        };
        var wasCalled = false;
        var action = new LifecycleAction(
            "test",
            _ => false,
            visualElement => wasCalled = ReferenceEquals(visualElement, element));

        await ElementLifecycleTracker.RunWhenDoneAsync(element, action);

        GC.KeepAlive(page);
        Assert.True(wasCalled);
    }

    [Fact]
    public async Task RunWhenDoneDoesNotRunWhenShellPageRemainsInNavigationStack()
    {
        var element = new Label();
        var page = new ContentPage
        {
            Content = element
        };
        var shell = new Shell
        {
            Items =
            {
                new ShellContent
                {
                    Content = new ContentPage()
                }
            }
        };
        await shell.Navigation.PushAsync(page, false);
        var wasCalled = false;
        var action = new LifecycleAction(
            "test",
            _ => false,
            _ => wasCalled = true);

        await ElementLifecycleTracker.RunWhenDoneAsync(element, action);

        GC.KeepAlive(shell);
        Assert.False(wasCalled);
    }

    [Fact]
    public async Task RunWhenDoneDoesNotRunWhenHostPageRemainsInShellContent()
    {
        var element = new Label();
        var page = new ContentPage
        {
            Content = element
        };
        var shell = new Shell
        {
            Items =
            {
                new ShellContent
                {
                    Content = page
                }
            }
        };
        var wasCalled = false;
        var action = new LifecycleAction(
            "test",
            _ => false,
            _ => wasCalled = true);

        await ElementLifecycleTracker.RunWhenDoneAsync(element, action);

        GC.KeepAlive(shell);
        Assert.False(wasCalled);
    }

    [Fact]
    public async Task RunWhenDoneRunsWhenShellPageLeavesNavigationStack()
    {
        var element = new Label();
        var page = new ContentPage
        {
            Content = element
        };
        var shell = new Shell
        {
            Items =
            {
                new ShellContent
                {
                    Content = new ContentPage()
                }
            }
        };
        await shell.Navigation.PushAsync(page, false);
        shell.Navigation.RemovePage(page);
        var wasCalled = false;
        var action = new LifecycleAction(
            "test",
            _ => false,
            visualElement => wasCalled = ReferenceEquals(visualElement, element));

        await ElementLifecycleTracker.RunWhenDoneAsync(element, action);

        GC.KeepAlive(shell);
        Assert.True(wasCalled);
    }
}
