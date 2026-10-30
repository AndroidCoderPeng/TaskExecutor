using System.Globalization;
using System.Windows.Data;

namespace TaskExecutor.Converters;

public class EnabledToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool enabled)
        {
            return enabled ? "已启用" : "已停用";
        }

        return "未知";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }
}