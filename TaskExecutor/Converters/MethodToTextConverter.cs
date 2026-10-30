using System.Globalization;
using System.Windows.Data;

namespace TaskExecutor.Converters;

public class MethodToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is List<string> notifyMethods)
        {
            return notifyMethods.Count != 0 ? string.Join(" + ", notifyMethods) : "无";
        }

        return "无";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }
}