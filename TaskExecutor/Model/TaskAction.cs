using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace TaskExecutor.Model;

public partial class TaskAction : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#FFFFFFFF";
    public string DimColor { get; set; } = "#FFFFFFFF";

    [ObservableProperty] private bool _isEnabled;

    /// <summary>
    /// 运行态标记：是否已在执行序列中（不落盘）
    /// </summary>
    [ObservableProperty] [JsonIgnore] private bool _inSequence;
}