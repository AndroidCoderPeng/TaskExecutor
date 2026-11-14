using CommunityToolkit.Mvvm.ComponentModel;

namespace TaskExecutor.Model;

public partial class DayOfWeek : ObservableObject
{
    public string Name { get; set; } = string.Empty;

    [ObservableProperty] private bool _isChecked;
}