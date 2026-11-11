using System.Globalization;
using System.Windows.Data;
using TaskExecutor.Cache;

namespace TaskExecutor.Converters;

public class TimeRangeConverter : IMultiValueConverter
{
    // 计划时间 + 随机偏移范围 → 实际执行窗口 "07:30 ~ 07:35"；无随机偏移时返回空串（准点）
    public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is [string { Length: > 0 } plannedAt, TimeRangeConfig range, ..]
            && TimeSpan.TryParseExact(plannedAt, [@"hh\:mm", @"h\:mm"], CultureInfo.InvariantCulture, out var baseTime))
        {
            var start = DateTime.Today.Add(baseTime).AddMinutes(range.MinLimitTime);
            var end = DateTime.Today.Add(baseTime).AddMinutes(range.MaxLimitTime);
            return $"{start:HH:mm} ~ {end:HH:mm}";
        }

        return string.Empty;
    }

    public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        return [];
    }
}