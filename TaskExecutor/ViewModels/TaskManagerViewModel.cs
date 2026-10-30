using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using TaskExecutor.Cache;
using TaskExecutor.Dialogs;
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
    [ObservableProperty] private ObservableCollection<LogRow> _runtimeLogs = [];

    #endregion

    private readonly string _adbPath = Path.Combine(AppContext.BaseDirectory, "ADB", "adb.exe");
    private readonly ConnectDeviceViewModel _deviceViewModel;
    private readonly IToastService _toastService;
    private readonly IDialogService _dialogService;

    public TaskManagerViewModel(ConnectDeviceViewModel deviceViewModel, IToastService toastService,
        IDialogService dialogService)
    {
        _deviceViewModel = deviceViewModel;
        _toastService = toastService;
        _dialogService = dialogService;


        // 进入页面时从唯一事实源拉取当前状态
        DeviceName = string.IsNullOrEmpty(deviceViewModel.DeviceName) ? "-" : deviceViewModel.DeviceName;
    }

    [RelayCommand]
    private async Task CreateTask()
    {
        // 需要二次检测设备是否被中途断开
        if (_deviceViewModel.ConnectionState != "已连接")
        {
            _toastService.Show("设备未连接，请先连接设备", ToastType.Warning);
            return;
        }

        if (!await IsDeviceAliveAsync())
        {
            _toastService.Show("设备连接已中断，请重新连接", ToastType.Error);

            // 归位状态 + 通知导航栏
            _deviceViewModel.MarkDisconnected();
            var message = new MessengerMessage.UpdateNavigationStateMessage(
                false, _deviceViewModel.DeviceName, _deviceViewModel.DeviceIp
            );
            WeakReferenceMessenger.Default.Send(message);
            return;
        }

        var systemVersion = await _deviceViewModel.GetDeviceSystemVersionAsync();
        var parameter = new KeyValuePair<string, string>(DeviceName, systemVersion);
        _dialogService.ShowDialog(typeof(CreateTaskDialog), parameter);
    }

    /// <summary>
    /// 向 adb 真实探测目标设备是否仍处于 device 状态。
    /// </summary>
    private async Task<bool> IsDeviceAliveAsync()
    {
        var lines = await RunAdbAsync("devices");
        var target = $"{_deviceViewModel.DeviceIp}:5555";

        return lines.Any(line =>
        {
            var parts = line.Split('\t');
            return parts.Length == 2
                   && parts[0].Trim() == target
                   && parts[1].Trim() == "device";
        });
    }

    /// <summary>
    /// 执行 adb 子命令，返回按行拆分的输出。
    /// </summary>
    private async Task<IReadOnlyList<string>> RunAdbAsync(string arguments)
    {
        var commandLine = new ArgumentCreator().Append(arguments).ToCommandLine();
        var (_, output) = await new CommandExecutor(commandLine).ExecuteWithOutputAsync(_adbPath);
        return output;
    }
}