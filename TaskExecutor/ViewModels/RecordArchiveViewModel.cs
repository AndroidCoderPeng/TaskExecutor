using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using TaskExecutor.Enums;
using TaskExecutor.Model;
using TaskExecutor.Services;

namespace TaskExecutor.ViewModels;

public partial class RecordArchiveViewModel : ObservableObject
{
    #region VM

    [ObservableProperty] private int _imageTotalCount;
    [ObservableProperty] private int _newImageCount;
    [ObservableProperty] private string _totalSize = "";
    [ObservableProperty] private string _totalSizeUnit = "KB";
    [ObservableProperty] private string _lastCaptureTime = "-";
    [ObservableProperty] private string _lastCaptureDay = ""; // 今天/昨天/MM-dd，卡片 4 的前置文字
    [ObservableProperty] private ObservableCollection<ScreenshotGroup> _dayGroups = [];

    #endregion

    private readonly string _screenshotDir = Path.Combine(AppContext.BaseDirectory, "Screenshots");
    private readonly IToastService _toastService;

    public RecordArchiveViewModel(IToastService toastService)
    {
        _toastService = toastService;
        Reload();
    }

    private void Reload()
    {
        var items = (Directory.Exists(_screenshotDir)
                ? Directory.EnumerateFiles(_screenshotDir, "*.png").Select(f => new FileInfo(f))
                : [])
            .Select(ToItem)
            .OrderByDescending(s => s.CaptureTime)
            .ToList();

        DayGroups =
        [
            .. items.GroupBy(item => item.CaptureTime.Date).Select(group => new ScreenshotGroup
            {
                Date = group.Key,
                Header = HeaderOf(group.Key),
                SizeText = FormatSize(group.Sum(s => s.SizeBytes)),
                Shots = [.. group],
            })
        ];

        ImageTotalCount = items.Count;
        NewImageCount = items.Count(s => s.CaptureTime.Date == DateTime.Today);

        var totalBytes = items.Sum(s => s.SizeBytes);
        (TotalSize, TotalSizeUnit) = FormatSizeParts(totalBytes);

        var last = items.FirstOrDefault();
        if (last is null)
        {
            LastCaptureTime = "-";
            LastCaptureDay = "";
        }
        else
        {
            LastCaptureDay = HeaderOf(last.CaptureTime.Date);
            LastCaptureTime = last.CaptureTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        }
    }

    private static ScreenshotItem ToItem(FileInfo file)
    {
        // 文件名即生成时间；解析失败回退到文件修改时间，保证异常文件也能显示
        var time = ParseCaptureTime(file.Name) ?? file.LastWriteTime;
        return new ScreenshotItem
        {
            FileName = file.Name,
            FullPath = file.FullName,
            CaptureTime = time,
            TimeText = time.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            SizeText = FormatSize(file.Length),
            SizeBytes = file.Length,
        };
    }

    private static DateTime? ParseCaptureTime(string fileName)
    {
        const string prefix = "ScreenCapture_";
        if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;

        var stamp = Path.GetFileNameWithoutExtension(fileName[prefix.Length..]);
        return stamp.Length >= 15 && DateTime.TryParseExact(stamp[..15],
            "yyyyMMdd_HHmmss", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var time)
            ? time
            : null;
    }

    private static string HeaderOf(DateTime date)
    {
        if (date.Date == DateTime.Today) return "今天";
        return date.Date == DateTime.Today.AddDays(-1) ? "昨天" : date.ToString("MM-dd", CultureInfo.InvariantCulture);
    }

    private static string FormatSize(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / 1024d / 1024d:F1} MB" : $"{Math.Round(bytes / 1024d)} KB";

    [RelayCommand]
    private void ImageItemDoubleClicked(ScreenshotItem? shot)
    {
        if (shot is null) return;
    }

    [RelayCommand]
    private void DeleteCapture(ScreenshotItem? shot)
    {
        if (shot is null) return;

        var result = MessageBox.Show(
            $"确定删除 {shot.CaptureTime:HH:mm:ss} 的截图吗？\n文件 {shot.FileName} 将从磁盘移除，此操作不可恢复。",
            "删除截图", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (result != MessageBoxResult.OK) return;

        try
        {
            File.Delete(shot.FullPath);
        }
        catch (Exception e)
        {
            // 文件被占用/已不存在：只报错，不影响页面其余数据
            _toastService.Show($"删除失败：{e.Message}", ToastType.Error);
            return;
        }

        var group = DayGroups.FirstOrDefault(g => g.Shots.Contains(shot));
        if (group != null)
        {
            group.Shots.Remove(shot);
            if (group.Shots.Count == 0) DayGroups.Remove(group);
            else group.SizeText = FormatSize(group.Shots.Sum(s => s.SizeBytes));
        }

        ImageTotalCount = DayGroups.Sum(g => g.Shots.Count);
        NewImageCount = DayGroups.Where(g => g.Date == DateTime.Today).Sum(g => g.Shots.Count);
        (TotalSize, TotalSizeUnit) = FormatSizeParts(DayGroups.Sum(g => g.Shots.Sum(s => s.SizeBytes)));

        var lastShot = DayGroups
            .SelectMany(g => g.Shots)
            .OrderByDescending(s => s.CaptureTime)
            .FirstOrDefault();
        if (lastShot is null)
        {
            LastCaptureTime = "-";
            LastCaptureDay = "";
        }
        else
        {
            LastCaptureDay = HeaderOf(lastShot.CaptureTime.Date);
            LastCaptureTime = lastShot.CaptureTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        _toastService.Show("截图已删除", ToastType.Success);
    }

    private static (string Value, string Unit) FormatSizeParts(long bytes) => bytes >= 1024 * 1024
        ? ($"{bytes / 1024d / 1024d:F1}", "MB")
        : ($"{Math.Round(bytes / 1024d)}", "KB");
}