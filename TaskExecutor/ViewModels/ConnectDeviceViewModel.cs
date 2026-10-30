using System.IO;
using System.Net;
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
    private async Task DisconnectDeviceAsync()
    {
        if (!File.Exists(_adbPath))
        {
            _toastService.Show("未找到 adb.exe", ToastType.Error);
            return;
        }

        await RunAdbWithOutputAsync("disconnect", $"{DeviceIp}:5555");

        DeviceName = string.Empty;
        ConnectionState = "未连接";
        _toastService.Show("设备已断开连接", ToastType.Success);
    }

    [RelayCommand]
    private async Task ConnectDeviceAsync()
    {
        if (string.IsNullOrWhiteSpace(DeviceIp) || !IPAddress.TryParse(DeviceIp, out _))
        {
            _toastService.Show("请输入有效的设备 IP 地址", ToastType.Error);
            return;
        }

        if (!File.Exists(_adbPath))
        {
            _toastService.Show("未找到 adb.exe", ToastType.Error);
            return;
        }

        _toastService.Show("正在连接设备...", ToastType.Info, 8000);
        ConnectionState = "连接中";

        try
        {
            // 枚举当前 adb 可见设备
            var devices = await RunAdbAsync("devices");
            var usbSerials = new List<string>();
            var unauthorized = false;

            foreach (var line in devices)
            {
                var parts = line.Split('\t');
                if (parts.Length != 2)
                {
                    // 跳过表头 "List of devices attached" 与空行
                    continue;
                }

                var serial = parts[0].Trim();
                var state = parts[1].Trim();

                // 序列号不含冒号 => USB 设备（网络设备的序列号是 ip:port）
                if (!serial.Contains(':'))
                {
                    if (state is "device" or "unauthorized" or "offline")
                    {
                        usbSerials.Add(serial);
                    }

                    if (state == "unauthorized")
                    {
                        unauthorized = true;
                    }
                }
            }

            // 多台设备直接拒绝后续流程
            if (usbSerials.Count > 1)
            {
                _toastService.Show("检测到多台 USB 设备，请仅连接一台后重试", ToastType.Warning);
                return;
            }

            // 设备未授权时 tcpip 会失败，先提示
            if (usbSerials.Count == 1 && unauthorized)
            {
                _toastService.Show("请在手机上允许 USB 调试授权后重试", ToastType.Warning);
                return;
            }

            // 插着 USB：切 TCP 模式，再等待 adbd 以网络方式重启
            if (usbSerials.Count == 1)
            {
                var (code, output) = await RunAdbWithOutputAsync("tcpip", "5555");

                if (code != 0 || !string.Join(" ", output).Contains("TCP mode"))
                {
                    _toastService.Show("设备切换 TCP 模式失败", ToastType.Error);
                    return;
                }

                await Task.Delay(2000); // 给 adbd 重启留时间，过早 connect 会被拒绝
            }

            // 无线连接（0 台 USB 设备时也走到这里：设备可能早已处于 tcpip 模式）
            var (_, connectOutput) = await RunAdbWithOutputAsync("connect", $"{DeviceIp}:5555");
            var connectResult = string.Join(" ", connectOutput);

            // "connected to" 同时覆盖 "already connected to"；失败时是 "failed to connect"
            if (!connectResult.Contains("connected to"))
            {
                _toastService.Show("连接失败，请先用 USB 数据线连接设备后重试", ToastType.Error);
                return;
            }

            // 成功：取机型、更新状态、持久化 IP
            DeviceName = await GetDeviceModelAsync();
            ConnectionState = "已连接";

            _config.Ip = DeviceIp;
            SettingsStore.Save(ConfigSections.FileName, ConfigSections.DeviceIp, _config);

            _toastService.Show("设备连接成功", ToastType.Success);
        }
        finally
        {
            // 唯一出口：只要离开时还是“连接中…”，说明走了失败/异常分支，归位到未连接
            if (ConnectionState == "连接中")
            {
                ConnectionState = "未连接";
            }
        }
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

    private async Task<(int Code, IReadOnlyList<string> Output)> RunAdbWithOutputAsync(params string[] arguments)
    {
        var commandLine = new ArgumentCreator();
        foreach (var argument in arguments)
        {
            commandLine.Append(argument);
        }

        return await new CommandExecutor(commandLine.ToCommandLine()).ExecuteWithOutputAsync(_adbPath);
    }

    private async Task<string> GetDeviceModelAsync()
    {
        var (_, output) = await RunAdbWithOutputAsync("shell", "getprop", "ro.product.model");
        return output.FirstOrDefault()?.Trim() ?? string.Empty;
    }
}