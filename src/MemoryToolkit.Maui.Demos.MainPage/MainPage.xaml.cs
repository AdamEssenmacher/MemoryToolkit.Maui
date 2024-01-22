namespace MemoryToolkit.Maui.Demos.MainPage;

public partial class MainPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private void PushLeakyPage(object? sender, EventArgs e)
    {
        Navigation.PushAsync(new LeakyPage());
    }

    private void SwapLeakyPage(object? sender, EventArgs e)
    {
        Application.Current!.MainPage = new LeakyPage();
    }

    private void PushSafePage(object? sender, EventArgs e)
    {
        Navigation.PushAsync(new SafePage());
    }

    private void SwapSafePage(object? sender, EventArgs e)
    {
        Application.Current!.MainPage = new SafePage();
    }
}