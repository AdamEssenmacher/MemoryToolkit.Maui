namespace MemoryToolkit.Maui.Demos.MainPage;

public partial class LeakyPage
{
    public LeakyPage()
    {
        InitializeComponent();
    }

    private void GoBack(object? sender, EventArgs e)
    {
        if (Navigation.NavigationStack.Count > 1)
            Navigation.PopAsync();
        else
            Application.Current!.MainPage = new NavigationPage(new MainPage());
    }
}