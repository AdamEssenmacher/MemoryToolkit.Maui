namespace MemoryToolkit.Maui.E2ETests;

public sealed class App : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new E2ETestPage());
    }
}
