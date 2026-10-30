using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskExecutor.Cache;
using TaskExecutor.Services;
using TaskExecutor.Utils;

namespace TaskExecutor.ViewModels;

public partial class CreateTaskDialogViewModel : ObservableObject, IDialogParameterReceiver, IDialogCloseRequester
{
    #region VM

    [ObservableProperty] private string _deviceName = string.Empty;
    [ObservableProperty] private string _systemVersion = string.Empty;
    [ObservableProperty] private string _taskName = string.Empty;
    [ObservableProperty] private string _taskTime = string.Empty;
    [ObservableProperty] private string _tips = string.Empty;
    [ObservableProperty] private bool _isRandomOffsetTime;
    [ObservableProperty] private bool _isNotifyByMail;
    [ObservableProperty] private bool _isNotifyByWechat;
    [ObservableProperty] private TargetAppRow? _selectedTargetApp;

    public ObservableCollection<TargetAppRow> TargetApps { get; } = [];

    #endregion

    public event Action<bool> CloseRequested = delegate { };

    public void OnDialogOpened(object? parameter)
    {
        if (parameter is KeyValuePair<string, string> pair)
        {
            DeviceName = pair.Key;
            SystemVersion = pair.Value;
        }
    }

    private readonly IToastService _toastService;
    private readonly TaskConfig _config;

    public CreateTaskDialogViewModel(IToastService toastService)
    {
        _toastService = toastService;

        _config = SettingsStore.Load<TaskConfig>(ConfigSections.FileName, ConfigSections.Task);

        var range = SettingsStore.Load<TimeRangeConfig>(ConfigSections.FileName, ConfigSections.TimeRange);
        Tips = $"执行时间将随机偏移 {range.LowerLimitTime}~{range.UpperLimitTime} 分钟";

        var appConfig = SettingsStore.Load<TargetAppConfig>(ConfigSections.FileName, ConfigSections.TargetApp);
        foreach (var app in appConfig.Apps)
        {
            TargetApps.Add(app);
        }
    }

    [RelayCommand]
    private void CancelDialog()
    {
        CloseRequested.Invoke(false);
    }

    [RelayCommand]
    private void SaveTask()
    {
        if (string.IsNullOrWhiteSpace(TaskName))
        {
            _toastService.Show("请输入任务名称", ToastType.Warning);
            return;
        }

        if (!TimeSpan.TryParseExact(TaskTime.Trim(), ["hh\\:mm", "h\\:mm"], CultureInfo.InvariantCulture,
                out var executeTime) || executeTime >= TimeSpan.FromDays(1))
        {
            _toastService.Show("执行时间格式不正确，应为 HH:MM", ToastType.Warning);
            return;
        }

        if (SelectedTargetApp == null || string.IsNullOrWhiteSpace(SelectedTargetApp.PackageName))
        {
            _toastService.Show("请选择目标 APP，可在「目标 APP」页维护映射", ToastType.Warning);
            return;
        }

        var notifyMethods = new List<string>();
        if (IsNotifyByMail) notifyMethods.Add("邮件");
        if (IsNotifyByWechat) notifyMethods.Add("企业微信");

        var timeRange = SettingsStore.Load<TimeRangeConfig>(ConfigSections.FileName, ConfigSections.TimeRange);

        _config.Tasks.Add(new TaskRow
        {
            Name = TaskName,
            TargetApp = new TargetAppRow
            {
                Name = SelectedTargetApp.Name,
                PackageName = SelectedTargetApp.PackageName,
                Activity = SelectedTargetApp.Activity
            },
            ExecuteTime = executeTime.ToString(@"hh\:mm"),
            TimeRange = IsRandomOffsetTime ? timeRange : null,
            NotifyMethods = notifyMethods,
            IsEnabled = true
        });
        SettingsStore.Save(ConfigSections.FileName, ConfigSections.Task, _config);

        _toastService.Show("任务创建成功", ToastType.Success);
        CloseRequested.Invoke(true);
    }
}
