using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskExecutor.Cache;
using TaskExecutor.Enums;
using TaskExecutor.Model;
using TaskExecutor.Services;
using TaskExecutor.Utils;

namespace TaskExecutor.ViewModels;

public partial class TaskDialogViewModel : ObservableObject, IDialogParameterReceiver, IDialogCloseRequester
{
    #region VM

    [ObservableProperty] private string _title = "新建任务";
    [ObservableProperty] private string _saveButtonText = "保存任务";
    [ObservableProperty] private string _deviceSummary = "未连接设备（任务将在设备在线时执行）";
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _taskName = string.Empty;
    [ObservableProperty] private string _plannedTime = string.Empty;
    [ObservableProperty] private string _tips = string.Empty;
    [ObservableProperty] private bool _isRandomOffsetTime;
    [ObservableProperty] private bool _isNotifyByMail;
    [ObservableProperty] private bool _isNotifyByWechat;
    [ObservableProperty] private TargetAppRow? _selectedTargetApp;

    [ObservableProperty] private ObservableCollection<TaskAction> _defaultActions =
    [
        new() { Name = "亮屏", Color = "#FF1FA84F", DimColor = "#1F1FA84F" },
        new() { Name = "打开应用", Color = "#FF8250C8", DimColor = "#1F8250C8" },
        new() { Name = "截屏", Color = "#FF0F9688", DimColor = "#1F0F9688" },
        new() { Name = "发送消息", Color = "#FFF5A500", DimColor = "#1FF5A500" },
        new() { Name = "关闭应用", Color = "#FFE5484D", DimColor = "#1FE5484D" },
        new() { Name = "息屏", Color = "#FF687386", DimColor = "#1F687386" }
    ];

    public ObservableCollection<TargetAppRow> TargetApps { get; } = [];

    #endregion

    public event Action<bool> CloseRequested = delegate { };

    private readonly IToastService _toastService;
    private readonly TaskConfig _config;
    private int _editingIndex = -1;

    public TaskDialogViewModel(IToastService toastService)
    {
        _toastService = toastService;

        _config = SettingsStore.Load<TaskConfig>(ConfigSections.FileName, ConfigSections.Task);

        var range = SettingsStore.Load<TimeRangeConfig>(ConfigSections.FileName, ConfigSections.TimeRange);
        Tips = $"计划时间将随机偏移 {range.MinLimitTime}~{range.MaxLimitTime} 分钟";

        var appConfig = SettingsStore.Load<TargetAppConfig>(ConfigSections.FileName, ConfigSections.TargetApp);
        foreach (var app in appConfig.Apps)
        {
            TargetApps.Add(app);
        }
    }

    public void OnDialogOpened(object? parameter)
    {
        if (parameter is not TaskDialogContext context) return;

        DeviceSummary = string.IsNullOrEmpty(context.DeviceName)
            ? "未连接设备（任务将在设备在线时执行）"
            : string.IsNullOrEmpty(context.SystemVersion)
                ? context.DeviceName
                : $"{context.DeviceName} · Android {context.SystemVersion}";

        // 下标越界（理论上不会，除非配置被外部改动）：降级为新建
        if (context.EditIndex < 0 || context.EditIndex >= _config.Tasks.Count) return;

        _editingIndex = context.EditIndex;
        var task = _config.Tasks[_editingIndex];

        IsEditing = true;
        Title = "编辑任务";
        SaveButtonText = "保存修改";
        TaskName = task.Name;
        PlannedTime = task.PlannedAt;
        IsRandomOffsetTime = task.TimeRange != null;
        IsNotifyByMail = task.NotifyMethod.Contains("邮件");
        IsNotifyByWechat = task.NotifyMethod.Contains("企业微信");
        SelectedTargetApp = MatchTargetApp(task.TargetApp);
    }

    private TargetAppRow MatchTargetApp(TargetAppRow target)
    {
        var matched =
            TargetApps.FirstOrDefault(app => app.PackageName == target.PackageName && app.Activity == target.Activity)
            ?? TargetApps.FirstOrDefault(app => app.PackageName == target.PackageName);

        if (matched != null) return matched;

        // 目标 APP 已在「目标 APP」页被删除：把任务里的旧值挂回列表，
        // 否则 ComboBox 选不中、保存时会被"请选择目标 APP"卡死
        var orphan = new TargetAppRow
        {
            Name = target.Name,
            PackageName = target.PackageName,
            Activity = target.Activity
        };
        TargetApps.Add(orphan);
        return orphan;
    }

    [RelayCommand]
    private void CancelDialog()
    {
        CloseRequested.Invoke(false);
    }

    [RelayCommand]
    private void SaveTask()
    {
        if (SelectedTargetApp == null || string.IsNullOrWhiteSpace(SelectedTargetApp.PackageName))
        {
            _toastService.Show("请选择目标 APP，可在「目标 APP」页维护映射", ToastType.Warning);
            return;
        }

        // 时间格式非法时调度器会 TryParseExact 失败并静默跳过，任务永远不会跑
        if (string.IsNullOrWhiteSpace(PlannedTime))
        {
            _toastService.Show("请选择计划时间", ToastType.Warning);
            return;
        }

        var isEditing = _editingIndex >= 0;
        // 名称是调度去重键（Name@date），创建后不可修改：仅新建时确定并校验唯一
        if (!isEditing)
        {
            if (string.IsNullOrWhiteSpace(TaskName)) TaskName = NextDefaultName();
            TaskName = TaskName.Trim();

            if (_config.Tasks.Any(t => t.Name == TaskName))
            {
                _toastService.Show($"已存在同名任务「{TaskName}」，请换一个名称", ToastType.Warning);
                return;
            }
        }

        var notifyMethod = IsNotifyByWechat ? "企业微信" : IsNotifyByMail ? "邮件" : "";
        var timeRange = SettingsStore.Load<TimeRangeConfig>(ConfigSections.FileName, ConfigSections.TimeRange);

        var task = isEditing ? _config.Tasks[_editingIndex] : new TaskRow { Name = TaskName, IsEnabled = true };

        task.TargetApp = new TargetAppRow
        {
            Name = SelectedTargetApp.Name,
            PackageName = SelectedTargetApp.PackageName,
            Activity = SelectedTargetApp.Activity
        };
        task.PlannedAt = PlannedTime;
        task.TimeRange = IsRandomOffsetTime ? timeRange : null;
        task.NotifyMethod = notifyMethod;

        if (!isEditing) _config.Tasks.Add(task);

        SettingsStore.Save(ConfigSections.FileName, ConfigSections.Task, _config);

        _toastService.Show(isEditing ? $"任务「{task.Name}」已更新" : "任务创建成功", ToastType.Success);
        CloseRequested.Invoke(true);
    }

    /// <summary>
    /// 空名时生成唯一默认名：「新任务」「新任务 2」…
    /// </summary>
    private string NextDefaultName()
    {
        var name = "新任务";
        var suffix = 2;
        while (_config.Tasks.Any(t => t.Name == name))
        {
            name = $"新任务 {suffix++}";
        }

        return name;
    }
}