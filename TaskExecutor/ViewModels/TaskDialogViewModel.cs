using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskExecutor.Cache;
using TaskExecutor.Services;
using TaskExecutor.Utils;

namespace TaskExecutor.ViewModels;

public partial class TaskDialogViewModel : ObservableObject, IDialogParameterReceiver, IDialogCloseRequester
{
    #region VM

    [ObservableProperty] private string _title = "新建任务";
    [ObservableProperty] private string _deviceSummary = "未连接设备（任务将在设备在线时执行）";
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
        if (parameter is TaskDialogContext context)
        {
            DeviceSummary = string.IsNullOrEmpty(context.DeviceName)
                ? "未连接设备（任务将在设备在线时执行）"
                : string.IsNullOrEmpty(context.SystemVersion)
                    ? context.DeviceName
                    : $"{context.DeviceName} · Android {context.SystemVersion}";

            if (context.Task != null)
            {
                Title = "编辑任务";
                TaskName = context.Task.Name;
                TaskTime = context.Task.ExecuteTime;
                IsRandomOffsetTime = context.Task.TimeRange != null;
                IsNotifyByMail = context.Task.NotifyMethod.Contains("邮件");
                IsNotifyByWechat = context.Task.NotifyMethod.Contains("企业微信");

                SelectedTargetApp = TargetApps.FirstOrDefault(app =>
                    app.PackageName == context.Task.TargetApp.PackageName &&
                    app.Activity == context.Task.TargetApp.Activity);
            }
        }
    }

    private readonly IToastService _toastService;
    private readonly TaskConfig _config;

    public TaskDialogViewModel(IToastService toastService)
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
            TaskName = "新任务";
        }

        if (SelectedTargetApp == null || string.IsNullOrWhiteSpace(SelectedTargetApp.PackageName))
        {
            _toastService.Show("请选择目标 APP，可在「目标 APP」页维护映射", ToastType.Warning);
            return;
        }

        var notifyMethod = "";
        if (IsNotifyByMail) notifyMethod = "邮件";
        if (IsNotifyByWechat) notifyMethod = "企业微信";

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
            ExecuteTime = TaskTime,
            TimeRange = IsRandomOffsetTime ? timeRange : null,
            NotifyMethod = notifyMethod,
            IsEnabled = true
        });
        SettingsStore.Save(ConfigSections.FileName, ConfigSections.Task, _config);

        _toastService.Show("任务创建成功", ToastType.Success);
        CloseRequested.Invoke(true);
    }
}