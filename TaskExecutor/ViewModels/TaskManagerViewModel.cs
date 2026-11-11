using System.Collections.ObjectModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Newtonsoft.Json;
using TaskExecutor.Cache;
using TaskExecutor.Dialogs;
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
    [ObservableProperty] private ObservableCollection<RuntimeLog> _runtimeLogs = [];

    #endregion

    private readonly ConnectDeviceViewModel _deviceViewModel;
    private readonly IToastService _toastService;
    private readonly IDialogService _dialogService;
    private readonly IAdbHeartbeatService _heartbeatService;
    private int _taskFilterOption;

    public TaskManagerViewModel(ConnectDeviceViewModel deviceViewModel, IToastService toastService,
        IDialogService dialogService, ISchedulerService schedulerService, ILogStoreService logStoreService,
        IAdbHeartbeatService heartbeatService)
    {
        _deviceViewModel = deviceViewModel;
        _toastService = toastService;
        _dialogService = dialogService;
        _heartbeatService = heartbeatService;

        DeviceName = string.IsNullOrEmpty(deviceViewModel.DeviceName) ? "-" : deviceViewModel.DeviceName;
        WeakReferenceMessenger.Default.Register<MessengerMessage.UpdateNavigationStateMessage>(this,
            (_, msg) => { DeviceName = string.IsNullOrEmpty(msg.Value.DeviceName) ? "-" : msg.Value.DeviceName; });

        var config = SettingsStore.Load<TaskConfig>(ConfigSections.FileName, ConfigSections.Task);
        Tasks = new ObservableCollection<TaskRow>(config.Tasks);

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

        // 进入页面时从唯一事实源拉取当前状态
        ApplyStatus(schedulerService.CurrentStatus);
    }

    /// <summary>
    /// 把调度器状态快照映射到界面属性。
    /// </summary>
    private void ApplyStatus(SchedulerStatus status)
    {
        IsTaskScheduled = status.IsScheduled;
        ScheduledTaskCount = status.TodayTotal;
        SuccessRate = status.SuccessRate;
        NextTaskTime = status.NextTaskTime?.ToString("HH:mm") ?? "-";
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
    private async Task EditTask(TaskRow task)
    {
        var index = Tasks.IndexOf(task);
        if (index < 0) return;
        
        // 因为是周期性执行任务，所以可以在主流程中直接修改任务项，等下次就会直接生效
        var systemVersion = _deviceViewModel.IsConnected
            ? await _deviceViewModel.GetDeviceSystemVersionAsync($"{_deviceViewModel.DeviceIp}:5555")
            : null;

        var parameter = new TaskDialogContext(_deviceViewModel.DeviceName, systemVersion, index);
        if (_dialogService.ShowDialog(typeof(TaskDialog), parameter) != true) return;

        ReloadTasks();
    }

    private void ReloadTasks()
    {
        var config = SettingsStore.Load<TaskConfig>(ConfigSections.FileName, ConfigSections.Task);
        Tasks = new ObservableCollection<TaskRow>(config.Tasks);
        ApplyTaskFilter();
    }
    
    [RelayCommand]
    private void ToggleTask(TaskRow task)
    {
        var index = Tasks.IndexOf(task);
        if (index < 0) return;

        task.IsEnabled = !task.IsEnabled;
        Tasks[index] = task; // 触发 Replace，让绑定重新求值

        SaveTasks();
        ApplyTaskFilter();

        _toastService.Show(
            task.IsEnabled ? $"任务「{task.Name}」已启用" : $"任务「{task.Name}」已暂停",
            task.IsEnabled ? ToastType.Success : ToastType.Info);
    }

    [RelayCommand]
    private void DeleteTask(TaskRow task)
    {
        if (!Tasks.Remove(task)) return;

        SaveTasks();
        ApplyTaskFilter();

        _toastService.Show($"任务「{task.Name}」已删除", ToastType.Success);
    }

    /// <summary>
    /// 把界面上的任务列表整体写回 app-config.json 的 Task 段。
    /// 调度器每 30s tick 都会重新加载配置，所以保存后无需重启、无需通知调度器。
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

    private void ApplyTaskFilter()
    {
        var view = CollectionViewSource.GetDefaultView(Tasks);
        view.Filter = _taskFilterOption switch
        {
            1 => o => o is TaskRow { IsEnabled: true },
            2 => o => o is TaskRow { IsEnabled: false },
            _ => null,
        };
    }
}