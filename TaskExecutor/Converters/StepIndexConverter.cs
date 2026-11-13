using System.Globalization;
using System.Windows.Data;

namespace TaskExecutor.Converters;

/// <summary>
/// AlternationIndex（0 起）→ 步骤序号（1 起）。
/// </summary>
public sealed class StepIndexConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int index ? $"第 {index + 1} 步" : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}