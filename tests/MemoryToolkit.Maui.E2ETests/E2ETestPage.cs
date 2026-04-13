namespace MemoryToolkit.Maui.E2ETests;

public sealed class E2ETestPage : ContentPage
{
    private readonly Label _statusLabel = new()
    {
        Text = "MemoryToolkit.Maui e2e tests are starting...",
        Padding = 24,
        LineBreakMode = LineBreakMode.WordWrap
    };

    private readonly ContentView _testHost = new();
    private bool _hasRun;

    public E2ETestPage()
    {
        Title = "MemoryToolkit E2E Tests";
        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            Children =
            {
                new ScrollView
                {
                    Content = _statusLabel
                },
                _testHost
            }
        };

        Grid.SetRow(_testHost, 1);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_hasRun)
            return;

        _hasRun = true;

        try
        {
            E2ETestReport report = await E2ETestRunner.RunAsync(this);
            await report.WriteAsync(E2ETestReport.ResolveOutputPath());

            _statusLabel.Text =
                $"E2E tests completed. Passed: {report.Passed}\n\nReport: {report.OutputPath}";

            if (ShouldExit())
            {
                await Task.Delay(250);
                Environment.Exit(report.Passed ? 0 : 1);
            }
        }
        catch (Exception exception)
        {
            _statusLabel.Text = $"E2E tests failed:\n{exception}";

            if (ShouldExit())
                Environment.Exit(1);
        }
    }

    public void AttachTestView(View view)
    {
        _testHost.Content = view;
    }

    public void ClearTestView()
    {
        _testHost.Content = null;
    }

    public Task WaitForUiAsync(int milliseconds = 150)
    {
        return Task.Delay(milliseconds);
    }

    private static bool ShouldExit()
    {
        return Environment.GetEnvironmentVariable("MEMORYTOOLKIT_E2E_EXIT") == "1";
    }
}
