namespace MemoryToolkit.Maui.Tests;

public sealed class MemoryToolkitOptionsTests
{
    [Fact]
    public void DefaultTearDownStrategyUsesMauiHandlerDisconnection()
    {
        var options = new MemoryToolkitOptions();

        Assert.Equal(TearDownStrategy.DisconnectHandlers, options.DefaultTearDownStrategy);
    }

    [Theory]
    [InlineData(TearDownStrategy.DetectOnly)]
    [InlineData(TearDownStrategy.DisconnectHandlers)]
    [InlineData(TearDownStrategy.Compartmentalize)]
    public void TearDownStrategyExposesExpectedV2Modes(TearDownStrategy strategy)
    {
        Assert.True(Enum.IsDefined(strategy));
    }
}
