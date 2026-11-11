using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace TaskExecutor.Controls;

public partial class TimePicker : UserControl
{
    private readonly string[] _hourItems = [.. Enumerable.Range(0, 24).Select(h => h.ToString("00"))];

    private List<string> _minuteItems = [];
    private bool _isSyncing;

    public TimePicker()
    {
        InitializeComponent();

        HourBox.ItemsSource = _hourItems;
        RebuildMinuteItems();
        ApplyTime(SelectedTime);
    }

    public static readonly DependencyProperty SelectedTimeProperty = DependencyProperty.Register(
        nameof(SelectedTime),
        typeof(TimeSpan),
        typeof(TimePicker),
        new FrameworkPropertyMetadata(
            TimeSpan.Zero,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnSelectedTimeChanged,
            CoerceSelectedTime));

    /// <summary>
    /// 选中时间，自动截断到分钟精度并限制在 00:00 ~ 23:59。
    /// </summary>
    public TimeSpan SelectedTime
    {
        get => (TimeSpan)GetValue(SelectedTimeProperty);
        set => SetValue(SelectedTimeProperty, value);
    }

    private static object CoerceSelectedTime(DependencyObject d, object baseValue)
    {
        var time = (TimeSpan)baseValue;

        if (time < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        if (time >= TimeSpan.FromDays(1))
        {
            return new TimeSpan(23, 59, 0);
        }

        return new TimeSpan(time.Hours, time.Minutes, 0);
    }

    private static void OnSelectedTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (TimePicker)d;
        picker.ApplyTime((TimeSpan)e.NewValue);
        picker.UpdateTimeText((TimeSpan)e.NewValue);
    }

    public static readonly DependencyProperty TimeTextProperty = DependencyProperty.Register(
        nameof(TimeText),
        typeof(string),
        typeof(TimePicker),
        new FrameworkPropertyMetadata(
            "00:00",
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnTimeTextChanged));

    public string TimeText
    {
        get => (string)GetValue(TimeTextProperty);
        set => SetValue(TimeTextProperty, value);
    }

    private static void OnTimeTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (TimePicker)d;

        if (picker._isSyncing)
        {
            return;
        }

        if (!TryParseTime(e.NewValue as string, out var time))
        {
            // 外部写入非法值时，回显当前合法时间，不破坏选中状态
            picker.UpdateTimeText(picker.SelectedTime);
            return;
        }

        picker.SetCurrentValue(SelectedTimeProperty, time);
    }

    private static bool TryParseTime(string? text, out TimeSpan time)
    {
        return TimeSpan.TryParseExact(text, @"hh\:mm", CultureInfo.InvariantCulture, out time) &&
               time < TimeSpan.FromDays(1);
    }
    
    public static readonly DependencyProperty MinuteStepProperty = DependencyProperty.Register(
        nameof(MinuteStep),
        typeof(int),
        typeof(TimePicker),
        new FrameworkPropertyMetadata(1, OnMinuteStepChanged),
        value => value is int step and >= 1 and <= 30);
    
    public int MinuteStep
    {
        get => (int)GetValue(MinuteStepProperty);
        set => SetValue(MinuteStepProperty, value);
    }
    
    private static void OnMinuteStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (TimePicker)d;
        picker.RebuildMinuteItems();
        picker.ApplyTime(picker.SelectedTime);
    }
    
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncing || HourBox.SelectedIndex < 0 || MinuteBox.SelectedIndex < 0)
        {
            return;
        }

        var hour = HourBox.SelectedIndex;
        var minute = int.Parse(_minuteItems[MinuteBox.SelectedIndex], CultureInfo.InvariantCulture);

        SetCurrentValue(SelectedTimeProperty, new TimeSpan(hour, minute, 0));
    }
    
    private void RebuildMinuteItems()
    {
        var step = Math.Clamp(MinuteStep, 1, 30);

        _minuteItems = [];
        for (var minute = 0; minute < 60; minute += step)
        {
            _minuteItems.Add(minute.ToString("00"));
        }

        if (MinuteBox is not null)
        {
            MinuteBox.ItemsSource = _minuteItems;
        }
    }

    private void ApplyTime(TimeSpan time)
    {
        if (HourBox is null || _minuteItems.Count == 0)
        {
            return;
        }

        _isSyncing = true;

        var hour = Math.Clamp(time.Hours, 0, 23);
        HourBox.SelectedIndex = hour;

        var minuteIndex = FindNearestMinuteIndex(time.Minutes);
        MinuteBox.SelectedIndex = minuteIndex;

        _isSyncing = false;

        var snapped = new TimeSpan(hour, int.Parse(_minuteItems[minuteIndex], CultureInfo.InvariantCulture), 0);
        if (snapped != time)
        {
            // 分钟不落在步长上时，吸附到最近的合法值并回写绑定源
            SetCurrentValue(SelectedTimeProperty, snapped);
        }
    }
    
    private int FindNearestMinuteIndex(int minute)
    {
        var bestIndex = 0;
        var bestDelta = int.MaxValue;

        for (var i = 0; i < _minuteItems.Count; i++)
        {
            var delta = Math.Abs(int.Parse(_minuteItems[i], CultureInfo.InvariantCulture) - minute);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                bestIndex = i;
            }
        }

        return bestIndex;
    }
    
    private void UpdateTimeText(TimeSpan time)
    {
        var text = $"{time.Hours:00}:{time.Minutes:00}";
        if (string.Equals(TimeText, text, StringComparison.Ordinal))
        {
            return;
        }

        _isSyncing = true;
        SetCurrentValue(TimeTextProperty, text);
        _isSyncing = false;
    }
}