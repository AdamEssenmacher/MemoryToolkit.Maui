namespace ShellSample;

public partial class LeakyLabel
{
    // ReSharper disable once NotAccessedField.Local
    private int _state = 0;
    
    public LeakyLabel()
    {
        InitializeComponent();
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        Application.Current!.PropertyChanged += (_, _) => _state++;
    }
}