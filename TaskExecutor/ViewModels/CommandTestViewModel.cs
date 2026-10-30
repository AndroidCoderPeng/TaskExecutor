using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskExecutor.Cache;
using TaskExecutor.Services;
using TaskExecutor.Utils;

namespace TaskExecutor.ViewModels;

public partial class CommandTestViewModel : ObservableObject
{
    #region VM

    [ObservableProperty] private string _deviceName = "-";

    #endregion

    private readonly string _adbPath = Path.Combine(AppContext.BaseDirectory, "ADB", "adb.exe");
    private readonly string _screenshotDir = Path.Combine(AppContext.BaseDirectory, "Screenshots");
    private readonly ConnectDeviceViewModel _deviceViewModel;
    private readonly IToastService _toastService;

    public CommandTestViewModel(ConnectDeviceViewModel deviceViewModel, IToastService toastService)
    {
        _deviceViewModel = deviceViewModel;
        _toastService = toastService;

        DeviceName = string.IsNullOrEmpty(deviceViewModel.DeviceName) ? "-" : deviceViewModel.DeviceName;
    }

    private string DeviceSerial => $"{_deviceViewModel.DeviceIp}:5555";

    [RelayCommand]
    private async Task WakeupPhoneAsync()
    {
        if (!CheckConnected()) return;

        var (code, _) = await RunAdbAsync("shell", "input", "keyevent", "KEYCODE_WAKEUP");
        _toastService.Show(code == 0 ? "已发送亮屏指令" : "亮屏指令执行失败", code == 0 ? ToastType.Success : ToastType.Error);
    }

    [RelayCommand]
    private async Task ScreenOffPhoneAsync()
    {
        if (!CheckConnected()) return;

        var (code, _) = await RunAdbAsync("shell", "input", "keyevent", "KEYCODE_SLEEP");
        _toastService.Show(code == 0 ? "已发送息屏指令" : "息屏指令执行失败", code == 0 ? ToastType.Success : ToastType.Error);
    }

    [RelayCommand]
    private async Task ScreencapAsync()
    {
        if (!CheckConnected()) return;

        Directory.CreateDirectory(_screenshotDir);
        var localPath = Path.Combine(_screenshotDir, $"screencap_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        var remotePath = "/sdcard/screencap_tmp.png";

        var (capCode, _) = await RunAdbAsync("shell", "screencap", "-p", remotePath);
        if (capCode != 0)
        {
            _toastService.Show("设备端截屏失败", ToastType.Error);
            return;
        }

        var (pullCode, _) = await RunAdbAsync("pull", remotePath, localPath);
        await RunAdbAsync("shell", "rm", remotePath); // 清理设备端临时文件

        _toastService.Show(pullCode == 0 ? $"截屏已保存到 {localPath}" : "截屏文件传输失败",
            pullCode == 0 ? ToastType.Success : ToastType.Error);
    }

    [RelayCommand]
    private async Task OpenTargetAppAsync()
    {
        if (!CheckConnected()) return;
        if (!TryGetTargetApp(out var app)) return;

        var (code, output) = await RunAdbAsync("shell", "am", "start", "-n", $"{app.PackageName}/{app.Activity}");
        var failed = code != 0 || output.Any(line => line.Contains("Error", StringComparison.OrdinalIgnoreCase));
        _toastService.Show(failed ? "启动目标应用失败" : $"已启动 {app.Name}", failed ? ToastType.Error : ToastType.Success);
    }

    [RelayCommand]
    private async Task ForceStopAppAsync()
    {
        if (!CheckConnected()) return;
        if (!TryGetTargetApp(out var app)) return;

        var (code, _) = await RunAdbAsync("shell", "am", "force-stop", app.PackageName);
        _toastService.Show(code == 0 ? $"已关闭 {app.Name}" : "关闭目标应用失败", code == 0 ? ToastType.Success : ToastType.Error);
    }

    private bool CheckConnected()
    {
        if (_deviceViewModel.ConnectionState == "已连接")
        {
            return true;
        }

        _toastService.Show("设备未连接，请先连接设备", ToastType.Warning);
        return false;
    }

    /// <summary>
    /// 执行 adb 子命令（自动附带 -s 指定目标设备），返回 (退出码, 输出行)。
    /// </summary>
    private async Task<(int Code, IReadOnlyList<string> Output)> RunAdbAsync(params string[] arguments)
    {
        var commandLine = new ArgumentCreator().Append("-s").Append(DeviceSerial);
        foreach (var argument in arguments)
        {
            commandLine.Append(argument);
        }

        return await new CommandExecutor(commandLine.ToCommandLine()).ExecuteWithOutputAsync(_adbPath);
    }

    private bool TryGetTargetApp(out TargetAppRow app)
    {
        var config = SettingsStore.Load<TargetAppConfig>(ConfigSections.FileName, ConfigSections.TargetApp);
        app = config.Apps.FirstOrDefault() ?? new TargetAppRow();

        if (string.IsNullOrWhiteSpace(app.PackageName))
        {
            _toastService.Show("未配置目标 APP，请在「目标 APP」页维护", ToastType.Warning);
            return false;
        }

        return true;
    }
}