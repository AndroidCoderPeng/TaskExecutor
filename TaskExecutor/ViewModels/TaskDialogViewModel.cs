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
        new() { Name = "亮屏", IsEnabled = true, Color = "#FF1FA84F", DimColor = "#1F1FA84F" },
        new() { Name = "打开应用", IsEnabled = true, Color = "#FF8250C8", DimColor = "#1F8250C8" },
        new() { Name = "截屏", IsEnabled = true, Color = "#FF0F9688", DimColor = "#1F0F9688" },
        new() { Name = "发送消息", Color = "#FFF5A500", DimColor = "#1FF5A500" }, // 选中渠道后解锁
        new() { Name = "关闭应用", IsEnabled = true, Color = "#FFE5484D", DimColor = "#1FFE5484D" },
        new() { Name = "息屏", IsEnabled = true, Color = "#FF687386", DimColor = "#1F687386" }
    ];

    [ObservableProperty] private ObservableCollection<TaskAction> _selectedActions = [];

    public ObservableCollection<TargetAppRow> TargetApps { get; } = [];

    #endregion

    public event Action<bool> CloseRequested = delegate { };

    private readonly IToastService _toastService;
    private readonly TaskConfig _config;
    private int _editingIndex = -1;

    public SequenceDragHandler DragSourceHandler { get; } = new();
    public SequenceDropHandler DropSourceHandler { get; }

    public TaskDialogViewModel(IToastService toastService)
    {
        _toastService = toastService;
        DropSourceHandler = new SequenceDropHandler(toastService);

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
        SelectedActions = [.. task.Actions];
        IsNotifyByMail = task.NotifyMethods.Contains("QQ 邮箱");
        IsNotifyByWechat = task.NotifyMethods.Contains("企业微信");
        SelectedTargetApp = MatchTargetApp(task.TargetApp);

        RefreshMessageAction();
        
        foreach (var action in DefaultActions)
        {
            action.InSequence = SelectedActions.Any(a => a.Name == action.Name);
        }
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
    private void ToggleNotifyChannel(string channel)
    {
        var enabled = channel == "QQ 邮箱" ? IsNotifyByMail : IsNotifyByWechat;

        if (enabled && !IsChannelConfigured(channel))
        {
            // 回弹：IsChecked 绑定已经把属性改成 true 了，翻回去
            if (channel == "QQ 邮箱") IsNotifyByMail = false;
            else IsNotifyByWechat = false;

            _toastService.Show(
                channel == "QQ 邮箱" ? "邮件渠道未配置，请先完成「邮件配置」" : "企业微信渠道未配置，请先填写 Webhook Key",
                ToastType.Warning);
        }

        RefreshMessageAction();
    }

    private void RefreshMessageAction()
    {
        var send = DefaultActions.First(a => a.Name == "发送消息");
        send.IsEnabled = IsNotifyByMail || IsNotifyByWechat;
    }

    private static bool IsChannelConfigured(string channel) => channel switch
    {
        "企业微信" => IsWechatConfigured(),
        "QQ 邮箱" => IsEmailConfigured(),
        _ => false
    };

    private static bool IsEmailConfigured()
    {
        var c = SettingsStore.Load<EmailConfig>(ConfigSections.FileName, ConfigSections.Email);
        return !string.IsNullOrWhiteSpace(c.SmtpServer)
               && c.SmtpPort > 0
               && !string.IsNullOrWhiteSpace(c.SenderAuthCode)
               && !string.IsNullOrWhiteSpace(c.RecipientEmail);
    }

    private static bool IsWechatConfigured()
    {
        var c = SettingsStore.Load<WechatConfig>(ConfigSections.FileName, ConfigSections.Wechat);
        return !string.IsNullOrWhiteSpace(c.WebhookKey);
    }

    [RelayCommand]
    private void SelectAction(TaskAction action)
    {
        if (!action.IsEnabled) return;   // 渠道未配置，Border 已禁用，兜底
        if (action.InSequence) return;   // 已选过，Border 已禁用，兜底
        
        SelectedActions.Add(new TaskAction
        {
            Name = action.Name,
            Color = action.Color,
            DimColor = action.DimColor,
            IsEnabled = true
        });
        
        action.InSequence = true; // 置灰动作库里的原条目
    }

    [RelayCommand]
    private void RemoveAction(TaskAction action)
    {
        SelectedActions.Remove(action);
        DefaultActions.First(a => a.Name == action.Name).InSequence = false;
    }

    [RelayCommand]
    private void ClearActions()
    {
        SelectedActions.Clear();
        foreach (var action in DefaultActions) action.InSequence = false;
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

        var notifyMethods = new List<string>();
        if (IsNotifyByMail) notifyMethods.Add("QQ 邮箱");
        if (IsNotifyByWechat) notifyMethods.Add("企业微信");
        var timeRange = SettingsStore.Load<TimeRangeConfig>(ConfigSections.FileName, ConfigSections.TimeRange);

        // 先加「发送消息」又把渠道关掉的兜底（放在写回配置之前，避免校验失败时污染配置）
        if (SelectedActions.Any(a => a.Name == "发送消息") && notifyMethods.Count == 0)
        {
            _toastService.Show("执行序列含「发送消息」，请至少选择一个通知方式", ToastType.Warning);
            return;
        }

        var task = isEditing ? _config.Tasks[_editingIndex] : new TaskRow { Name = TaskName, IsEnabled = true };

        task.TargetApp = new TargetAppRow
        {
            Name = SelectedTargetApp.Name,
            PackageName = SelectedTargetApp.PackageName,
            Activity = SelectedTargetApp.Activity
        };
        task.PlannedAt = PlannedTime;
        task.TimeRange = IsRandomOffsetTime ? timeRange : null;
        task.Actions = [.. SelectedActions];
        task.NotifyMethods = notifyMethods;

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