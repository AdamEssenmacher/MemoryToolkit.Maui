namespace ShellSample;

public partial class App
{
    public App()
    {
        InitializeComponent();

        MainPage = new AppShell();
    }

    public int LeaksDetected { get; set; }
}