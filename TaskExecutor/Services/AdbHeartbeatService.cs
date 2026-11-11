using System.IO;
using System.Windows.Threading;
using TaskExecutor.Enums;
using TaskExecutor.Model;
using TaskExecutor.Utils;

namespace TaskExecutor.Services;

/// <summary>
/// adb 连接心跳：每 30s 探测一次设备是否在线。
/// 掉线时先尝试一次 adb connect 自愈，连续 2 次失败才判定掉线（抗 Wi-Fi 抖动）。
/// 使用 DispatcherTimer，Beat 在 UI 线程触发，订阅方可直接更新绑定属性。
/// </summary>
public class AdbHeartbeatService : IAdbHeartbeatService
{
    private const int IntervalSeconds = 30;
    private const int FailureThreshold = 2; // 连续失败 2 次才判定掉线

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(IntervalSeconds) };
    private readonly string _adbPath = Path.Combine(AppContext.BaseDirectory, "ADB", "adb.exe");
    private readonly ILogStoreService _logStore;

    private string? _serial;
    private int _consecutiveFailures;
    private bool _isAlive;
    private bool _isBusy;
    private int _generation; // Start/Stop 代次，防止过期的探测结果回写状态

    public event Action<IAdbHeartbeatService.AdbHeartbeatResult>? Beat;
    public event Action<DateTime?>? HeartbeatUpdated;

    private DateTime? _lastSuccessTime;
    public DateTime? LastSuccessTime => _lastSuccessTime;

    public bool IsRunning => _serial != null;

    public AdbHeartbeatService(ILogStoreService logStore)
    {
        _logStore = logStore;
        _timer.Tick += OnTick;
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        if (_isBusy || _serial is not { } serial) return;

        _isBusy = true;
        var generation = _generation;

        try
        {
            // 在线：不做任何通知，避免每次心跳刷界面
            if (await ProbeAsync(serial))
            {
                if (generation != _generation) return;
                _consecutiveFailures = 0;
                MarkSuccess(serial);
                return;
            }

            if (generation != _generation) return;

            // adb connect 重试成功后同样算一次有效心跳
            await new CommandExecutor(new ArgumentCreator().Append("connect").Append(serial).ToCommandLine())
                .ExecuteWithOutputAsync(_adbPath);

            if (await ProbeAsync(serial))
            {
                if (generation != _generation) return;
                _consecutiveFailures = 0;
                MarkSuccess(serial);
                return;
            }

            if (generation != _generation) return;

            // 连续失败达到阈值才判定掉线，避免单次抖动误报
            _consecutiveFailures++;
            if (_consecutiveFailures < FailureThreshold || !_isAlive) return;

            _isAlive = false;
            AppendLog(TaskState.Failed, $"{IAdbHeartbeatService.LogPrefix}：设备连接已断开（{serial}）");
            Beat?.Invoke(new IAdbHeartbeatService.AdbHeartbeatResult(false, serial, "设备连接已断开"));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"adb 心跳执行异常：{exception.Message}");
        }
        finally
        {
            _isBusy = false;
        }
    }

    /// <summary>
    /// 掉线后恢复：只在状态确实从掉线翻回在线时通知。
    /// </summary>
    private void RaiseIfRestored(string serial)
    {
        if (_isAlive) return;

        _isAlive = true;
        AppendLog(TaskState.Success, $"{IAdbHeartbeatService.LogPrefix}：设备已恢复连接（{serial}）");
        Beat?.Invoke(new IAdbHeartbeatService.AdbHeartbeatResult(true, serial, "设备已恢复连接"));
    }

    private void AppendLog(TaskState state, string content) => _logStore.Append(new RuntimeLog
    {
        Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        State = state,
        Content = content
    });

    public void Start(string serial)
    {
        _serial = serial;
        _isAlive = true; // 连接流程刚成功，以在线为基线
        _consecutiveFailures = 0;
        _generation++;
        _timer.Start();

        // 连接成功即视为一次有效心跳，界面立刻从 0s 开始计时
        _lastSuccessTime = DateTime.Now;
        HeartbeatUpdated?.Invoke(_lastSuccessTime);
    }

    public void Stop()
    {
        _serial = null;
        _isAlive = false;
        _consecutiveFailures = 0;
        _generation++;
        _timer.Stop();

        _lastSuccessTime = null;
        HeartbeatUpdated?.Invoke(null);
    }

    /// <summary>
    /// 探测设备是否处于 device 状态；adb 不可用或异常一律视为不可用。
    /// </summary>
    public async Task<bool> ProbeAsync(string serial)
    {
        if (!File.Exists(_adbPath)) return false;

        try
        {
            var commandLine = new ArgumentCreator().Append("devices").ToCommandLine();
            var (code, output) = await new CommandExecutor(commandLine).ExecuteWithOutputAsync(_adbPath);
            if (code != 0) return false;

            return output.Any(line =>
            {
                var parts = line.Split('\t');
                return parts.Length == 2
                       && parts[0].Trim() == serial
                       && parts[1].Trim() == "device";
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"adb 心跳探测失败：{e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 探测成功：记录最近成功时间并通知界面，掉线后恢复时再触发 Beat
    /// </summary>
    /// <param name="serial"></param>
    private void MarkSuccess(string serial)
    {
        _lastSuccessTime = DateTime.Now;
        HeartbeatUpdated?.Invoke(_lastSuccessTime);
        RaiseIfRestored(serial);
    }
}