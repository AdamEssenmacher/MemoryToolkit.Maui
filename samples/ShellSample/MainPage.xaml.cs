using System.Collections.Immutable;

namespace ShellSample;

public partial class MainPage
{
    private readonly Random _random = new();
    private bool _isInStack = true;

    public MainPage()
    {
        InitializeComponent();

        ListView.ItemsSource = Enumerable.Range(1, 100)
            .Select(_ => new ListItem { ImageUrl = $"https://picsum.photos/seed/{_random.Next(1000)}/640/360" })
            .ToImmutableArray();

        Dispatcher.StartTimer(TimeSpan.FromSeconds(0.7), () =>
        {
            Dispatcher.Dispatch(RefreshStats);
            return _isInStack;
        });
    }

    private void RefreshStats()
    {
        double heapSize = GC.GetTotalMemory(false)
                          / 1024d;

        LeakCountLabel.Text = $"Leaks Detected: {((App)Application.Current!).LeaksDetected}";
        HeapSizeLabel.Text = $"Heap Size: {heapSize:n2} KB";
    }

    private void Push(object? sender, EventArgs e)
    {
        Navigation.PushAsync(new MainPage());
    }

    private void Pop(object? sender, EventArgs e)
    {
        Navigation.PopAsync();
        _isInStack = false;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshStats();
    }
}

public class ListItem
{
    public ListItem()
    {
        LoadMockedData();
    }

    public string? ImageUrl { get; set; }
    public byte[]? MockData { get; private set; }

    private void LoadMockedData()
    {
        // 300 KB mocked VM data payload
        Task.Run(() => MockData = Enumerable.Range(0, 1024 * 300).Select(_ => (byte)0xFF).ToArray());
    }
}