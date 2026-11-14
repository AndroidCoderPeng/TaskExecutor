namespace TaskExecutor.Model;

/// <summary>单张截图：文件名即生成时间（screencap_yyyyMMdd_HHmmssfff.png）。</summary>
public class ScreenshotItem
{
    public string FileName { get; set; } = "";
    public string FullPath { get; set; } = "";
    public DateTime CaptureTime { get; set; }
    public string TimeText { get; set; } = "";  // HH:mm:ss，缩略图下缘
    public string SizeText { get; set; } = "";  // "161 KB"
    public long SizeBytes { get; set; }
}