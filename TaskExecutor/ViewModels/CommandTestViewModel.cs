using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskExecutor.Cache;
using TaskExecutor.Enums;
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

    private AdbDevice CreateDevice() => new(_adbPath, DeviceSerial);

    [RelayCommand]
    private async Task WakeupPhoneAsync()
    {
        if (!CheckConnected()) return;

        var ok = await CreateDevice().ScreenOnAsync();
        _toastService.Show(ok ? "亮屏指令执行成功" : "亮屏指令执行失败", ok ? ToastType.Success : ToastType.Error);
    }

    [RelayCommand]
    private async Task ScreenOffPhoneAsync()
    {
        if (!CheckConnected()) return;

        var ok = await CreateDevice().ScreenOffAsync();
        _toastService.Show(ok ? "已发送息屏指令" : "息屏指令执行失败", ok ? ToastType.Success : ToastType.Error);
    }

    [RelayCommand]
    private async Task ScreencapAsync()
    {
        if (!CheckConnected()) return;

        var localPath = await CreateDevice().ScreencapAsync(_screenshotDir);
        _toastService.Show(localPath == null ? "截屏失败" : $"截屏已保存到 {localPath}",
            localPath == null ? ToastType.Error : ToastType.Success);
    }

    [RelayCommand]
    private async Task OpenTargetAppAsync()
    {
        if (!CheckConnected()) return;
        if (!TryGetTargetApp(out var app)) return;

        var ok = await CreateDevice().OpenAppAsync(app.PackageName, app.Activity);
        _toastService.Show(ok ? $"已启动 {app.Name}" : "启动目标应用失败", ok ? ToastType.Success : ToastType.Error);
    }

    [RelayCommand]
    private async Task ForceStopAppAsync()
    {
        if (!CheckConnected()) return;
        if (!TryGetTargetApp(out var app)) return;

        var ok = await CreateDevice().KillAppAsync(app.PackageName);
        _toastService.Show(ok ? $"已关闭 {app.Name}" : "关闭目标应用失败", ok ? ToastType.Success : ToastType.Error);
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