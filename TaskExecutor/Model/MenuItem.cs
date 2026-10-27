using CommunityToolkit.Mvvm.ComponentModel;

namespace TaskExecutor.Model;

public partial class MenuItem : ObservableObject
{
    public string Group { get; set; }
    public string Icon { get; set; }
    public string Title { get; set; }

    [ObservableProperty] private bool _isEnabled;
}