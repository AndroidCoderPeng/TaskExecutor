using CommunityToolkit.Mvvm.ComponentModel;

namespace TaskExecutor.Model;

public partial class MenuItem : ObservableObject
{
    public required string Group { get; set; }
    public required string Icon { get; set; }
    public required string Title { get; set; }

    [ObservableProperty] private bool _isEnabled;
}