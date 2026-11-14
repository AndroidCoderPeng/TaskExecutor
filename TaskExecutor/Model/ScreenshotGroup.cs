using System.Collections.ObjectModel;

namespace TaskExecutor.Model;

/// <summary>按天分组的截图集合（ListBox 的行）。</summary>
public class ScreenshotGroup
{
    public DateTime Date { get; set; }
    public string Header { get; set; } = ""; // 今天 / 昨天 / 11-13
    public string SizeText { get; set; } = "";
    public ObservableCollection<ScreenshotItem> Shots { get; set; } = [];
}