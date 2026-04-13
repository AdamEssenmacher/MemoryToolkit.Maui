namespace MemoryToolkit.Maui.Tests;

public sealed class AttachedBehaviorPropertyTests
{
    [Fact]
    public void LeakMonitorBehaviorAttachedPropertiesRoundTrip()
    {
        var label = new Label();

        Assert.False(LeakMonitorBehavior.GetCascade(label));
        Assert.False(LeakMonitorBehavior.GetSuppress(label));
        Assert.Null(LeakMonitorBehavior.GetName(label));

        LeakMonitorBehavior.SetCascade(label, true);
        LeakMonitorBehavior.SetSuppress(label, true);
        LeakMonitorBehavior.SetName(label, "tracked-label");

        Assert.True(LeakMonitorBehavior.GetCascade(label));
        Assert.True(LeakMonitorBehavior.GetSuppress(label));
        Assert.Equal("tracked-label", LeakMonitorBehavior.GetName(label));

        LeakMonitorBehavior.SetCascade(label, false);

        Assert.False(LeakMonitorBehavior.GetCascade(label));
    }

    [Fact]
    public void TearDownBehaviorAttachedPropertiesRoundTrip()
    {
        var label = new Label();

        Assert.False(TearDownBehavior.GetCascade(label));
        Assert.False(TearDownBehavior.GetSuppress(label));
        Assert.Null(TearDownBehavior.GetStrategy(label));

        TearDownBehavior.SetCascade(label, true);
        TearDownBehavior.SetSuppress(label, true);
        TearDownBehavior.SetStrategy(label, TearDownStrategy.Compartmentalize);

        Assert.True(TearDownBehavior.GetCascade(label));
        Assert.True(TearDownBehavior.GetSuppress(label));
        Assert.Equal(TearDownStrategy.Compartmentalize, TearDownBehavior.GetStrategy(label));

        TearDownBehavior.SetCascade(label, false);
        TearDownBehavior.SetStrategy(label, null);

        Assert.False(TearDownBehavior.GetCascade(label));
        Assert.Null(TearDownBehavior.GetStrategy(label));
    }

    [Fact]
    public void CascadePropertiesRejectNonVisualElements()
    {
        var bindableObject = new TestBindableObject();

        Assert.Throws<InvalidOperationException>(() => LeakMonitorBehavior.SetCascade(bindableObject, true));
        Assert.Throws<InvalidOperationException>(() => TearDownBehavior.SetCascade(bindableObject, true));
    }

    private sealed class TestBindableObject : BindableObject
    {
    }
}
