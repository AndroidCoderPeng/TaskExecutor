using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TaskExecutor.Cache;

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
    [ObservableProperty] private ObservableCollection<LogRow> _runtimeLogs = [];

    #endregion

    public TaskManagerViewModel(ConnectDeviceViewModel device)
    {
        // 进入页面时从唯一事实源拉取当前状态
        DeviceName = string.IsNullOrEmpty(device.DeviceName) ? "-" : device.DeviceName;
    }
}