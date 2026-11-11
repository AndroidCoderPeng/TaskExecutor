using System.IO;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using TaskExecutor.Cache;
using TaskExecutor.Services;
using TaskExecutor.Utils;

namespace TaskExecutor.ViewModels;

public partial class ConnectDeviceViewModel : ObservableObject, IDeviceStateProvider
{
    /// <summary>
    /// adb devices 的一行记录；serial 含冒号的是网络连接（ip:port）。
    /// </summary>
    private sealed record AdbDeviceEntry(string Serial, string State);
    
    #region VM

    [ObservableProperty] private string _deviceName = string.Empty;
    [ObservableProperty] private string _deviceIp = string.Empty;
    [ObservableProperty] private string _connectionState = "未连接";

    #endregion

    // adb devices 中处于可用状态的标记
    private const string DeviceState = "device";
    
    public bool IsConnected => ConnectionState == "已连接";
    private readonly IToastService _toastService;
    private readonly IAdbHeartbeatService _heartbeatService;
    private readonly string _adbPath = Path.Combine(AppContext.BaseDirectory, "ADB", "adb.exe");
    private readonly DeviceIpConfig _config;

    public ConnectDeviceViewModel(IToastService toastService, IAdbHeartbeatService heartbeatService)
    {
        _toastService = toastService;
        _heartbeatService = heartbeatService;
        _heartbeatService.Beat += OnHeartbeat;

        _config = SettingsStore.Load<DeviceIpConfig>(ConfigSections.FileName, ConfigSections.DeviceIp);
        DeviceIp = _config.Ip;
    }

    /// <summary>
    /// 心跳状态翻转：掉线只归位界面状态（不弹 Toast，避免无人值守时刷屏），
    /// 恢复时自动回到「已连接」并补齐机型名。心跳在 UI 线程触发。
    /// </summary>
    private async void OnHeartbeat(IAdbHeartbeatService.AdbHeartbeatResult result)
    {
        try
        {
            if (result.IsAlive)
            {
                if (IsConnected) return;

                ConnectionState = "已连接";
                if (string.IsNullOrEmpty(DeviceName))
                {
                    DeviceName = await GetDeviceModelAsync($"{DeviceIp}:5555");
                }

                WeakReferenceMessenger.Default.Send(
                    new MessengerMessage.UpdateNavigationStateMessage(true, DeviceName, DeviceIp));
                return;
            }

            if (!IsConnected) return; // 已归位过，避免重复

            // 先留一份机型名，MarkDisconnected 会清空 DeviceName
            var name = DeviceName;
            MarkDisconnected();
            WeakReferenceMessenger.Default.Send(
                new MessengerMessage.UpdateNavigationStateMessage(false, name, DeviceIp));
        }
        catch (Exception e)
        {
            Console.WriteLine($"处理 adb 心跳结果失败：{e.Message}");
        }
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
        _heartbeatService.Stop(); // 主动断开：停心跳，避免 30s 后被自动连回来

        if (!File.Exists(_adbPath))
        {
            _toastService.Show("未找到 adb.exe", ToastType.Error);
            return;
        }

        await RunAdbWithOutputAsync("disconnect", $"{DeviceIp}:5555");

        MarkDisconnected();
        _toastService.Show("设备已断开连接", ToastType.Success);

        // 更新侧边导航栏状态
        WeakReferenceMessenger.Default.Send(
            new MessengerMessage.UpdateNavigationStateMessage(false, DeviceName, DeviceIp));
    }

    /// <summary>
    /// 将连接状态归位为未连接（断开连接、二次检测失败时调用）。
    /// </summary>
    public void MarkDisconnected()
    {
        DeviceName = string.Empty;
        ConnectionState = "未连接";
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

        var target = $"{DeviceIp}:5555";
        
        try
        {
            var entries = await ListDevicesAsync();
            
            // 目标无线连接已就绪：直接成功，绝不再执行 tcpip（避免重启 adbd 打断同一台手机的无线连接）
            var ready = entries.Any(e => e.Serial == target && e.State == DeviceState);
            var usbDevices = entries.Where(e => !e.Serial.Contains(':')).ToList();
            var otherNetwork = entries
                .Where(e => e.Serial.Contains(':') && e.Serial != target && e.State == DeviceState)
                .ToList();

            // 真·多设备才拒绝：同一台手机的 USB + 无线只是两条记录，不算多设备
            if (usbDevices.Count > 1 || otherNetwork.Count > 0)
            {
                var detail = string.Join("、", usbDevices.Select(d => d.Serial).Concat(otherNetwork.Select(d => d.Serial)));
                _toastService.Show($"检测到多台设备（{detail}），请只保留一台手机后重试", ToastType.Warning);
                return;
            }

            if (!ready && usbDevices.Count == 1)
            {
                var usb = usbDevices[0];

                if (usb.State == "unauthorized")
                {
                    _toastService.Show("请在手机上允许 USB 调试授权后重试", ToastType.Warning);
                    return;
                }

                if (usb.State != DeviceState)
                {
                    _toastService.Show($"USB 设备状态异常（{usb.State}），请重新插拔数据线后重试", ToastType.Warning);
                    return;
                }

                // USB 在线但无线未连：切 TCP 模式（会重启 adbd，之后必须等它重新上线）
                var (code, output) = await RunAdbWithOutputAsync("tcpip", "5555");
                if (code != 0 || !string.Join(" ", output).Contains("TCP mode"))
                {
                    _toastService.Show("设备切换 TCP 模式失败", ToastType.Error);
                    return;
                }

                await Task.Delay(1000);
            }
            
            if (!ready)
            {
                // 不信任 "connected to" 文本（"already connected to" 也可能是离线残留），只认 devices 里的真实状态
                await RunAdbWithOutputAsync("connect", target);
                ready = await WaitForDeviceAsync(target, TimeSpan.FromSeconds(6));
            }
            
            if (!ready)
            {
                _toastService.Show("连接失败，请确认手机与电脑处于同一局域网，或先用 USB 数据线连接一次后重试",
                    ToastType.Error);
                return;
            }

            // 成功：取机型、更新状态、持久化 IP
            DeviceName = await GetDeviceModelAsync(target);
            ConnectionState = "已连接";

            _config.Ip = DeviceIp;
            SettingsStore.Save(ConfigSections.FileName, ConfigSections.DeviceIp, _config);

            _toastService.Show("设备连接成功", ToastType.Success);
            _heartbeatService.Start($"{DeviceIp}:5555");

            // 更新侧边导航栏状态
            WeakReferenceMessenger.Default.Send(
                new MessengerMessage.UpdateNavigationStateMessage(true, DeviceName, DeviceIp));
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
    /// 解析 adb devices 输出（跳过表头与空行），得到 serial + 状态。
    /// </summary>
    private async Task<List<AdbDeviceEntry>> ListDevicesAsync()
    {
        var lines = await RunAdbAsync("devices");
        return lines
            .Select(line => line.Split('\t'))
            .Where(parts => parts.Length == 2)
            .Select(parts => new AdbDeviceEntry(parts[0].Trim(), parts[1].Trim()))
            .ToList();
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

    /// <summary>
    /// 轮询等待指定 serial 变为 device 状态（tcpip / connect 之后 adbd 需要时间恢复）。
    /// </summary>
    private async Task<bool> WaitForDeviceAsync(string serial, TimeSpan timeout)
    {
        var deadline = DateTime.Now + timeout;
        while (DateTime.Now < deadline)
        {
            var entries = await ListDevicesAsync();
            if (entries.Any(e => e.Serial == serial && e.State == DeviceState))
            {
                return true;
            }

            await Task.Delay(500);
        }

        return false;
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

    /// <summary>
    /// 读取设备机型；必须带 -s，否则 USB 与无线同时在线时 adb 会报 more than one device。
    /// </summary>
    private async Task<string> GetDeviceModelAsync(string serial)
    {
        var (_, output) = await RunAdbWithOutputAsync("-s", serial, "shell", "getprop", "ro.product.model");
        return output.FirstOrDefault()?.Trim() ?? string.Empty;
    }
    
    /// <summary>
    /// 读取系统版本；同样必须带 -s，避免多设备时命令失败。
    /// </summary>
    public async Task<string> GetDeviceSystemVersionAsync(string serial)
    {
        var (_, output) = await RunAdbWithOutputAsync("-s", serial, "shell", "getprop", "ro.build.version.release");
        return output.FirstOrDefault()?.Trim() ?? string.Empty;
    }
}