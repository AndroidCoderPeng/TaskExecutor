using System.Globalization;
using System.Windows.Data;
using TaskExecutor.Cache;

namespace TaskExecutor.Converters;

public class TimeRangeConverter : IMultiValueConverter
{
    public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is [string executeTime, TimeRangeConfig range, ..])
        {
            return $"{executeTime} + {range.LowerLimitTime}~{range.UpperLimitTime}min";
        }

        return values.Length > 0 ? values[0] : null;
    }

    public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        return [];
    }
}