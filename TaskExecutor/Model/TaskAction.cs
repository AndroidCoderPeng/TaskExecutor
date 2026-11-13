using CommunityToolkit.Mvvm.ComponentModel;

namespace TaskExecutor.Model;

public partial class TaskAction : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#FFFFFFFF";
    public string DimColor { get; set; } = "#FFFFFFFF";

    [ObservableProperty] private bool _isEnabled;
}