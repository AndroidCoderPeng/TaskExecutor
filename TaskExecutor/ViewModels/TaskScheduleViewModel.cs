using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json.Linq;
using TaskExecutor.Cache;
using TaskExecutor.Enums;
using TaskExecutor.Services;
using DayOfWeek = TaskExecutor.Model.DayOfWeek;

namespace TaskExecutor.ViewModels;

public partial class TaskScheduleViewModel : ObservableObject
{
    #region VM

    [ObservableProperty] private bool _isSkipHoliday = true;
    [ObservableProperty] private bool _isResetDailyTask = true;
    [ObservableProperty] private ObservableCollection<DayOfWeek> _weekdays = [];
    [ObservableProperty] private string _lastSyncTime = string.Empty;
    [ObservableProperty] private string _currentYear = string.Empty;

    #endregion

    private readonly IToastService _toastService;
    private readonly ISchedulerService _schedulerService;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly List<string> _cdnUrlTemplates =
    [
        "https://cdn.jsdelivr.net/npm/chinese-days/dist/years/%s.json",
        "https://fastly.jsdelivr.net/npm/chinese-days/dist/years/%s.json",
        "https://registry.npmmirror.com/chinese-days/latest/files/dist/years/%s.json"
    ];

    private static readonly string[] WeekdayNames = ["星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "星期日"];
    private static readonly string[] DefaultWeekdays = ["星期一", "星期二", "星期三", "星期四", "星期五"];

    // 恢复默认时批量改 IsChecked，避免每个 item 各触发一次落盘
    private bool _suppressWorkdaySave;

    public TaskScheduleViewModel(IToastService toastService, ISchedulerService schedulerService)
    {
        _toastService = toastService;
        _schedulerService = schedulerService;

        var config = SettingsStore.Load<TaskScheduleConfig>(ConfigSections.FileName, ConfigSections.TaskSchedule);
        IsSkipHoliday = config.IsSkipHoliday;
        IsResetDailyTask = config.IsResetDailyTask;

        var workday = SettingsStore.Load<WorkdayConfig>(ConfigSections.FileName, ConfigSections.Workday);
        InitWeekdays(workday.IsConfigured ? workday.Weekdays : DefaultWeekdays);
        if (!workday.IsConfigured)
        {
            SaveWorkday(); // 首次运行落盘默认值
        }

        CurrentYear = DateTime.Now.Year.ToString();
        LastSyncTime = SettingsStore.Load<ChinaHolidayConfig>(
            ConfigSections.FileName, ConfigSections.ChinaHoliday
        ).LastSyncTime;
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IsSkipHoliday) or nameof(IsResetDailyTask))
            {
                config.IsSkipHoliday = IsSkipHoliday;
                config.IsResetDailyTask = IsResetDailyTask;
                SettingsStore.Save(ConfigSections.FileName, ConfigSections.TaskSchedule, config);
                schedulerService.RefreshPlan();
            }
        };
    }

    private void InitWeekdays(IReadOnlyCollection<string> checkedNames)
    {
        Weekdays = new ObservableCollection<DayOfWeek>(WeekdayNames.Select(str => new DayOfWeek
        {
            Name = str, IsChecked = checkedNames.Contains(str)
        }));

        foreach (var day in Weekdays)
        {
            day.PropertyChanged += (_, e) =>
            {
                if (!_suppressWorkdaySave && e.PropertyName == nameof(DayOfWeek.IsChecked))
                {
                    SaveWorkday();
                }
            };
        }
    }

    private void SaveWorkday()
    {
        SettingsStore.Save(ConfigSections.FileName, ConfigSections.Workday, new WorkdayConfig
        {
            IsConfigured = true,
            Weekdays = [.. Weekdays.Where(day => day.IsChecked).Select(day => day.Name)]
        });
        _schedulerService.RefreshPlan();
    }

    [RelayCommand]
    private void ResetWorkday()
    {
        _suppressWorkdaySave = true;
        foreach (var day in Weekdays)
        {
            day.IsChecked = DefaultWeekdays.Contains(day.Name);
        }
        _suppressWorkdaySave = false;
        SaveWorkday();
        _toastService.Show("已恢复默认工作日", ToastType.Success);
    }

    [RelayCommand]
    private async Task SyncHolidayDataAsync()
    {
        var year = DateTime.Now.Year;

        foreach (var template in _cdnUrlTemplates)
        {
            var url = template.Replace("%s", year.ToString());
            try
            {
                var json = await Http.GetStringAsync(url);
                if (JToken.Parse(json) is not JObject { Count: > 0 }) continue;

                var syncTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                SettingsStore.Save(ConfigSections.FileName, ConfigSections.ChinaHoliday, new ChinaHolidayConfig
                {
                    Year = year,
                    LastSyncTime = syncTime,
                    Data = json
                });
                CurrentYear = year.ToString();
                LastSyncTime = syncTime;
                _toastService.Show("节假日数据同步成功", ToastType.Success);
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine($"节假日数据同步失败：{url}，{e.Message}");
            }
        }

        _toastService.Show("节假日数据同步失败，请检查网络后重试", ToastType.Error);
    }
}