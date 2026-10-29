using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace TaskExecutor.Model;

public partial class FaqItem : ObservableObject
{
    [JsonProperty(nameof(Question))] public required string Question { get; set; }

    [JsonProperty(nameof(Answer))] public required string Answer { get; set; }

    [JsonIgnore] // 展开状态是 UI 运行时状态，不写入 JSON
    [ObservableProperty]
    private bool _isExpanded;
}