using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Newtonsoft.Json;
using TaskExecutor.Cache;
using TaskExecutor.Dialogs;
using TaskExecutor.Enums;
using TaskExecutor.Model;
using TaskExecutor.Services;
using TaskExecutor.Utils;

namespace TaskExecutor.ViewModels;

public partial class TaskManagerViewModel : ObservableObject
{
    #region VM

    [ObservableProperty] private string _deviceName = "-";
    [ObservableProperty] private int _scheduledTaskCount;
    [ObservableProperty] private int _successRate;
    [ObservableProperty] private string _nextTaskTime = "-";
    [ObservableProperty] private ObservableCollection<TaskRow> _tasks = [];
    [ObservableProperty] private bool _isTaskScheduled;
    [ObservableProperty] private string _chainProgress = "";
    [ObservableProperty] private ObservableCollection<RuntimeLog> _runtimeLogs = [];
    [ObservableProperty] private bool _isRestBadgeVisible;
    [ObservableProperty] private bool _isRestBadgeWarning;
    [ObservableProperty] private string _restBadgeText = "";
    [ObservableProperty] private string _restBadgeTip = "";

    #endregion

    private readonly ConnectDeviceViewModel _deviceViewModel;
    private readonly IToastService _toastService;
    private readonly IDialogService _dialogService;
    private readonly ISchedulerService _schedulerService;
    private SchedulerStatus _schedulerStatus = new();
    private int _taskFilterOption;

    public TaskManagerViewModel(ConnectDeviceViewModel deviceViewModel, IToastService toastService,
        IDialogService dialogService, ISchedulerService schedulerService, ILogStoreService logStoreService)
    {
        _deviceViewModel = deviceViewModel;
        _toastService = toastService;
        _dialogService = dialogService;
        _schedulerService = schedulerService;

        DeviceName = string.IsNullOrEmpty(deviceViewModel.DeviceName) ? "-" : deviceViewModel.DeviceName;
        WeakReferenceMessenger.Default.Register<MessengerMessage.UpdateNavigationStateMessage>(this,
            (_, msg) => { DeviceName = string.IsNullOrEmpty(msg.Value.DeviceName) ? "-" : msg.Value.DeviceName; });

        WeakReferenceMessenger.Default.Register<MessengerMessage.TasksChangedMessage>(this, (_, _) => ReloadTasks());

        var config = SettingsStore.Load<TaskConfig>(ConfigSections.FileName, ConfigSections.Task);
        Tasks = [.. config.Tasks];

        // 日志以持久化存储为唯一事实源：先回填当天历史，再订阅增量
        foreach (var log in logStoreService.LoadByDate(DateOnly.FromDateTime(DateTime.Now)))
        {
            RuntimeLogs.Insert(0, log);
        }

        logStoreService.Appended += log =>
        {
            Console.WriteLine(JsonConvert.SerializeObject(log));
            RuntimeLogs.Insert(0, log);
        };
        schedulerService.StatusChanged += ApplyStatus;
        schedulerService.ChainProgressChanged += delegate(string taskName, string actionName)
        {
            var task = Tasks.FirstOrDefault(t => t.Name == taskName);
            if (task != null)
            {
                task.IsExecuting = actionName != "";
            }

            ChainProgress = actionName == "" ? "" : $"任务：{taskName} - 正在执行「{actionName}」";
        };

        // 进入页面时从唯一事实源拉取当前状态
        ApplyStatus(schedulerService.CurrentStatus);
    }

    /// <summary>
    /// 把调度器状态快照映射到界面属性。
    /// </summary>
    private void ApplyStatus(SchedulerStatus status)
    {
        _schedulerStatus = status;
        IsTaskScheduled = status.IsScheduled;
        ScheduledTaskCount = status.TodayTotal;
        SuccessRate = status.SuccessRate;
        NextTaskTime = status.NextTaskTime is { } next
            ? next.ToString(next.Date == DateTime.Today ? "HH:mm" : "MM-dd HH:mm", CultureInfo.InvariantCulture)
            : "-";
        RefreshRestBadge();
        ApplyTaskFilter();
    }

    [RelayCommand]
    private async Task CreateTask()
    {
        var systemVersion = _deviceViewModel.IsConnected
            ? await _deviceViewModel.GetDeviceSystemVersionAsync($"{_deviceViewModel.DeviceIp}:5555")
            : null;

        var parameter = new TaskDialogContext(_deviceViewModel.DeviceName, systemVersion);
        if (_dialogService.ShowDialog(typeof(TaskDialog), parameter) != true) return;

        ReloadTasks();
    }

    [RelayCommand]
    private void ToggleTask(TaskRow task)
    {
        var index = Tasks.IndexOf(task);
        if (index < 0) return;

        task.IsEnabled = !task.IsEnabled;
        Tasks[index] = task; // 触发 Replace，让绑定重新求值

        SaveTasks();
        _toastService.Show(
            task.IsEnabled ? $"任务「{task.Name}」已启用" : $"任务「{task.Name}」已暂停",
            task.IsEnabled ? ToastType.Success : ToastType.Info);
        _schedulerService.RefreshPlan();
        ApplyTaskFilter();
    }

    [RelayCommand]
    private async Task RunTaskOnce(TaskRow task)
    {
        if (!_deviceViewModel.IsConnected)
        {
            _toastService.Show("设备未连接，请先连接设备", ToastType.Warning);
            return;
        }

        if (!task.IsEnabled)
        {
            _toastService.Show($"任务「{task.Name}」已暂停，请先启用后再执行", ToastType.Warning);
            return;
        }

        _toastService.Show($"任务「{task.Name}」开始执行");
        await _schedulerService.RunOnceAsync(task);
    }

    [RelayCommand]
    private async Task EditTask(TaskRow task)
    {
        var index = Tasks.IndexOf(task);
        if (index < 0) return;

        var wasSkipped = _schedulerStatus.PlannedTasks.Any(plan =>
            plan.TaskName == task.Name && plan.State == PlannedTaskState.Skipped
        );
        var wasTriggered = _schedulerStatus.PlannedTasks.Any(plan =>
            plan.TaskName == task.Name && plan.State == PlannedTaskState.Triggered
        );

        // 因为是周期性执行任务，所以可以在主流程中直接修改任务项，等下次就会直接生效
        var systemVersion = _deviceViewModel.IsConnected
            ? await _deviceViewModel.GetDeviceSystemVersionAsync($"{_deviceViewModel.DeviceIp}:5555")
            : null;

        var parameter = new TaskDialogContext(_deviceViewModel.DeviceName, systemVersion, index);
        if (_dialogService.ShowDialog(typeof(TaskDialog), parameter) != true) return;

        ReloadTasks();
        if (!wasSkipped && _schedulerStatus.PlannedTasks.Any(plan => plan.TaskName == task.Name
                                                                     && plan.State == PlannedTaskState.Skipped
                                                                     && plan.ScheduledAt.Date >= DateTime.Today))
        {
            _toastService.Show($"任务「{task.Name}」的新计划时间已过，本周期跳过", ToastType.Warning);
        }
        else if (wasTriggered && _schedulerStatus.PlannedTasks.Any(plan => plan.TaskName == task.Name
                                                                          && plan.State == PlannedTaskState.Triggered))
        {
            // 仅当新时间仍在过去、未恢复待执行时提示；改到未来会直接重排，无需打扰
            _toastService.Show($"任务「{task.Name}」今日已执行，新计划时间仍已过，将于重置后生效");
        }
    }

    private void ReloadTasks()
    {
        var config = SettingsStore.Load<TaskConfig>(ConfigSections.FileName, ConfigSections.Task);
        Tasks = [.. config.Tasks];
        _schedulerService.RefreshPlan();
        ApplyTaskFilter();
    }

    [RelayCommand]
    private void DeleteTask(TaskRow task)
    {
        if (!Tasks.Remove(task)) return;

        _schedulerService.CancelChain(task.Name);

        SaveTasks();
        _toastService.Show($"任务「{task.Name}」已删除", ToastType.Success);
        _schedulerService.RefreshPlan();
        ApplyTaskFilter();
    }

    /// <summary>
    /// 把界面上的任务列表整体写回 app-config.json 的 Task 段。
    /// 修改后刷新调度器计划；调度器也会定期重新加载配置。
    /// </summary>
    private void SaveTasks()
    {
        var config = SettingsStore.Load<TaskConfig>(ConfigSections.FileName, ConfigSections.Task);
        config.Tasks = [.. Tasks];
        SettingsStore.Save(ConfigSections.FileName, ConfigSections.Task, config);
    }

    [RelayCommand]
    private void TaskOptionRadioChanged(string parameter)
    {
        _taskFilterOption = int.Parse(parameter);
        ApplyTaskFilter();
    }

    /// <summary>
    /// 工作日胶囊（与设计稿 restBadge 三态对齐）：
    /// 工作日 → 隐藏；休息日 +「跳过节假日」开启 → 橙色「休息日 · 任务暂停」；
    /// 休息日 + 未开启 → 灰色「今日休息日（跳过节假日未开启）」。
    /// </summary>
    private void RefreshRestBadge()
    {
        if (!HolidayHelper.IsRestDay(DateOnly.FromDateTime(DateTime.Now)))
        {
            IsRestBadgeVisible = false;
            return;
        }

        var skip = SettingsStore.Load<TaskScheduleConfig>(
            ConfigSections.FileName, ConfigSections.TaskSchedule
        ).IsSkipHoliday;
        IsRestBadgeVisible = true;
        IsRestBadgeWarning = skip;
        RestBadgeText = skip ? "休息日 · 任务暂停" : "今日休息日（跳过节假日未开启）";
        RestBadgeTip = skip
            ? "今日为休息日，「跳过节假日」已开启，到点任务不会触发"
            : "今日为休息日，「跳过节假日」未开启，任务正常执行";
    }

    private void ApplyTaskFilter()
    {
        foreach (var task in Tasks)
        {
            var plans = _schedulerStatus.PlannedTasks.Where(p => p.TaskName == task.Name).ToArray();
            var plan = plans.FirstOrDefault(p => p.State == PlannedTaskState.Pending)
                       ?? plans.LastOrDefault();
            task.ExecuteTime = plan?.ScheduledAt;
        }

        var view = CollectionViewSource.GetDefaultView(Tasks);
        if (view is ListCollectionView listView) listView.CustomSort = TaskPlanComparer.Instance;
        view.Filter = _taskFilterOption switch
        {
            1 => o => o is TaskRow { IsEnabled: true },
            2 => o => o is TaskRow { IsEnabled: false },
            _ => null,
        };
        view.Refresh();
    }

    /// <summary>
    /// 按计划时间升序、任务名升序排序，空时间排在最后。
    /// </summary>
    private sealed class TaskPlanComparer : IComparer
    {
        public static readonly TaskPlanComparer Instance = new();

        public int Compare(object? x, object? y)
        {
            if (x is not TaskRow a || y is not TaskRow b) return 0;
            if (a.ExecuteTime == null && b.ExecuteTime != null) return 1;
            if (a.ExecuteTime != null && b.ExecuteTime == null) return -1;
            var comparison = Nullable.Compare(a.ExecuteTime, b.ExecuteTime);
            return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(a.Name, b.Name);
        }
    }
}