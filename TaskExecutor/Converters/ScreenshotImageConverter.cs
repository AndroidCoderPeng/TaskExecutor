using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace TaskExecutor.Converters;

/// <summary>
/// 路径 → 解码完成的冻结 BitmapImage。
/// BitmapCacheOption.OnLoad 在解码后立即关闭文件流，
/// 避免 WPF 默认加载方式锁住图片文件，导致删除时报"正被另一进程使用"。
/// </summary>
public class ScreenshotImageConverter : IValueConverter
{
    // 同一路径只解码一次；缩略图滚动/换行重排时不再重复读盘
    private static readonly Dictionary<string, BitmapImage> Cache = [];

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || !File.Exists(path)) return null;
        if (Cache.TryGetValue(path, out var cached)) return cached;

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.UriSource = new Uri(path, UriKind.Absolute);
        bmp.EndInit();
        bmp.Freeze(); // 冻结：彻底释放句柄 + 跨线程安全 + 渲染性能更好
        Cache[path] = bmp;
        return bmp;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}