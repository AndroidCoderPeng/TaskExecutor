using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskExecutor.Cache;
using TaskExecutor.Services;
using TaskExecutor.Utils;

namespace TaskExecutor.ViewModels;

public partial class ConnectDeviceViewModel : ObservableObject
{
    #region VM

    [ObservableProperty] private string _deviceName = string.Empty;
    [ObservableProperty] private string _deviceIp = string.Empty;
    [ObservableProperty] private string _connectionState = "未连接";

    #endregion

    private readonly IToastService _toastService;
    private readonly string _adbPath = Path.Combine(AppContext.BaseDirectory, "ADB", "adb.exe");
    private readonly DeviceIpConfig _config;
    
    public ConnectDeviceViewModel(IToastService toastService)
    {
        _toastService = toastService;
        
        _config = SettingsStore.Load<DeviceIpConfig>(ConfigSections.FileName, ConfigSections.DeviceIp);
        DeviceIp = _config.Ip;
    }

    [RelayCommand]
    private async Task RestartAdbAsync()
    {
        if (!File.Exists(_adbPath))
        {
            _toastService.Show("未找到 adb.exe", ToastType.Error);
            return;
        }

        _toastService.Show("正在重启 adb...");

        var killArgs = new ArgumentCreator().Append("kill-server").ToCommandLine();
        var killCode = await new CommandExecutor(killArgs).ExecuteAsync(_adbPath);

        var startArgs = new ArgumentCreator().Append("start-server").ToCommandLine();
        var startCode = await new CommandExecutor(startArgs).ExecuteAsync(_adbPath);

        _toastService.Show(
            killCode == 0 && startCode == 0 ? "adb 服务已重启" : "adb 服务重启失败",
            killCode == 0 && startCode == 0 ? ToastType.Success : ToastType.Error);
    }

    [RelayCommand]
    private void DisconnectDevice()
    {
    }

    [RelayCommand]
    private void ConnectDevice()
    {
        
        
        // _config.Ip = DeviceIp;
        // SettingsStore.Save(ConfigSections.FileName, ConfigSections.DeviceIp, _config);
        // _toastService.Show("设备连接成功", ToastType.Success);
    }
}