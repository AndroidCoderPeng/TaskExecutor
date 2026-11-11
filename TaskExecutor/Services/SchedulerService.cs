using System.Globalization;
using System.IO;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using TaskExecutor.Cache;
using TaskExecutor.Model;
using TaskExecutor.Utils;
using TaskExecutor.ViewModels;

namespace TaskExecutor.Services;

public class SchedulerService : ISchedulerService
{
    private const string TimeFormat = "hh\\:mm";

    // 30s 一次 tick，UI 线程回调，方便直接更新 VM
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly string _adbPath = Path.Combine(AppContext.BaseDirectory, "ADB", "adb.exe");
    private readonly ConnectDeviceViewModel _deviceViewModel;

    private HashSet<string> _executedKeys = []; // key = $"{taskName}@yyyy-MM-dd"
    private Dictionary<string, int> _randomOffsets = new(); // 每任务当天只抽一次随机偏移(分钟)
    private DateOnly _currentDate;
    private DateOnly _resetAppliedDate; // ResetTime 当天是否已应用过
    private bool _isExecuting; // 防重入（串行执行期间跳过 tick）

    public event Action<RuntimeLog>? TaskExecuted; // 供 TaskManagerViewModel 记日志/刷统计
    public event Action<DateTime?>? NextTaskTimeChanged;
    public bool IsScheduled { get; private set; }

    public SchedulerService(ConnectDeviceViewModel deviceViewModel)
    {
        _deviceViewModel = deviceViewModel;
        _timer.Tick += OnTick;
    }

    public void Start()
    {
        if (IsScheduled) return;

        _currentDate = DateOnly.FromDateTime(DateTime.Now);
        IsScheduled = true;
        _timer.Start();
    }

    public void Stop()
    {
        IsScheduled = false;
        _timer.Stop();
    }

    private async void OnTick(object? s, EventArgs e)
    {
        if (_isExecuting) return;

        _isExecuting = true;
        try
        {
            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);

            // 跨天重置：清空已执行标记、重抽随机偏移
            if (today != _currentDate)
            {
                _currentDate = today;
                _executedKeys = [];
                _randomOffsets = [];
                _resetAppliedDate = default;
            }
            else
            {
                // ResetTime 到点重置（IsResetDailyTask 开启时才清空，实现当日可二次运行）
                var reset = SettingsStore.Load<ResetTimeConfig>(ConfigSections.FileName, ConfigSections.ResetTime);
                if (_resetAppliedDate != today
                    && TimeSpan.TryParseExact(reset.Time, [TimeFormat], CultureInfo.InvariantCulture, out var resetTime)
                    && now.TimeOfDay >= resetTime)
                {
                    _resetAppliedDate = today;

                    var schedule =
                        SettingsStore.Load<TaskScheduleConfig>(ConfigSections.FileName, ConfigSections.TaskSchedule);
                    if (schedule.IsResetDailyTask)
                    {
                        _executedKeys = [];
                        _randomOffsets = [];
                    }
                }
            }

            var scheduleConfig =
                SettingsStore.Load<TaskScheduleConfig>(ConfigSections.FileName, ConfigSections.TaskSchedule);

            // TODO 节假日和休息日需要分开考虑
            if (scheduleConfig.IsSkipHoliday && IsHoliday(today))
            {
                return;
            }

            // 每次 tick 重新加载配置：新建/编辑/启停任务无需重启即生效
            var taskConfig = SettingsStore.Load<TaskConfig>(ConfigSections.FileName, ConfigSections.Task);
            var globalRange = SettingsStore.Load<TimeRangeConfig>(ConfigSections.FileName, ConfigSections.TimeRange);

            // 收集今天已到点且未执行的任务
            var dueTasks = new List<(TaskRow Task, DateTime ScheduledAt)>();
            foreach (var task in taskConfig.Tasks.Where(t => t.IsEnabled))
            {
                if (!TimeSpan.TryParseExact(task.ExecuteTime, [TimeFormat], CultureInfo.InvariantCulture, out var time))
                {
                    continue;
                }

                var scheduledAt = today.ToDateTime(TimeOnly.FromTimeSpan(time));
                if (scheduleConfig.IsNeedRandomTime)
                {
                    scheduledAt = scheduledAt.AddMinutes(GetRandomOffset(task.Name, task.TimeRange ?? globalRange));
                }

                if (scheduledAt > now) continue;
                // Add 返回 false 说明 key 已存在 => 今天已执行过
                if (!_executedKeys.Add($"{task.Name}@{today:yyyy-MM-dd}")) continue;

                dueTasks.Add((task, scheduledAt));
            }

            // 按实际触发时刻排序，串行执行（任务之间隔 WaitTime 秒）
            foreach (var (task, _) in dueTasks.OrderBy(x => x.ScheduledAt))
            {
                await ExecuteTaskAsync(task);
            }

            // 广播下一个待执行时刻
            NextTaskTimeChanged?.Invoke(GetNextTaskTime(taskConfig, scheduleConfig, globalRange, today, now));
        }
        finally
        {
            _isExecuting = false;
        }
    }

    /// <summary>
    /// 执行单个任务：探测设备 → adb 拉起目标 APP → 发送通知 → 等待任务间隔。
    /// </summary>
    private async Task ExecuteTaskAsync(TaskRow task)
    {
        var serial = $"{_deviceViewModel.DeviceIp}:5555";

        // 设备离线：记失败日志并跳过，不反复重试
        if (_deviceViewModel.ConnectionState != "已连接" || !await IsDeviceAliveAsync(serial))
        {
            RaiseLog(false, $"任务「{task.Name}」执行失败：设备离线");
            return;
        }

        var commandLine = new ArgumentCreator()
            .Append("-s")
            .Append(serial)
            .Append("shell")
            .Append("am")
            .Append("start")
            .Append("-n")
            .Append($"{task.TargetApp.PackageName}/{task.TargetApp.Activity}")
            .ToCommandLine();
        var (code, output) = await new CommandExecutor(commandLine).ExecuteWithOutputAsync(_adbPath);

        var failed = code != 0 || output.Any(line => line.Contains("Error", StringComparison.OrdinalIgnoreCase));
        RaiseLog(!failed,
            failed
                ? $"任务「{task.Name}」执行失败：启动 {task.TargetApp.Name} 失败"
                : $"任务「{task.Name}」执行成功：已启动 {task.TargetApp.Name}");

        if (!failed)
        {
            await NotifyAsync(task, $"任务「{task.Name}」执行成功");
        }

        var wait = SettingsStore.Load<WaitTimeConfig>(ConfigSections.FileName, ConfigSections.WaitTime);
        if (wait.Time > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(wait.Time));
        }
    }

    /// <summary>
    /// 按 NotifyMethods 逐个发送通知，单渠道失败不影响其它渠道。
    /// </summary>
    private async Task NotifyAsync(TaskRow task, string content)
    {
        try
        {
            switch (task.NotifyMethod)
            {
                case "邮件":
                    await SendMailAsync(content);
                    break;
                case "企业微信":
                    await SendWechatAsync(content);
                    break;
            }
        }
        catch (Exception e)
        {
            RaiseLog(false, $"任务「{task.Name}」{task.NotifyMethod}通知发送失败：{e.Message}");
        }
    }

    private async Task SendMailAsync(string content)
    {
        var config = SettingsStore.Load<EmailConfig>(ConfigSections.FileName, ConfigSections.Email);
        if (string.IsNullOrWhiteSpace(config.SmtpServer) || string.IsNullOrWhiteSpace(config.RecipientEmail))
        {
            return; // 邮件配置不完整时静默跳过
        }

        await EmailSender.SendAsync(new EmailMessage
        {
            Host = config.SmtpServer,
            Port = config.SmtpPort,
            SenderEmail = config.SenderEmail,
            AuthCode = config.SenderAuthCode,
            Recipient = config.RecipientEmail,
            Subject = "TaskExecutor 任务执行通知",
            Body = content
        });
    }

    private async Task SendWechatAsync(string content)
    {
        var config = SettingsStore.Load<WechatConfig>(ConfigSections.FileName, ConfigSections.Wechat);
        if (string.IsNullOrWhiteSpace(config.WebhookKey))
        {
            return;
        }

        await WechatSender.SendTextAsync(new WechatTextMessage
        {
            WebhookKey = config.WebhookKey,
            Content = string.IsNullOrWhiteSpace(config.MessageTitle)
                ? content
                : $"{config.MessageTitle}\n{content}"
        });
    }

    /// <summary>
    /// 每任务每天只抽一次随机偏移并缓存，避免每 30s tick 不断重抽导致时刻漂移。
    /// </summary>
    private int GetRandomOffset(string taskName, TimeRangeConfig? range)
    {
        var key = $"{taskName}@{_currentDate:yyyy-MM-dd}";
        if (_randomOffsets.TryGetValue(key, out var offset))
        {
            return offset;
        }

        var lower = Math.Max(0, range?.LowerLimitTime ?? 0);
        var upper = Math.Max(lower, range?.UpperLimitTime ?? 0);
        offset = Random.Shared.Next(lower, upper + 1);
        _randomOffsets[key] = offset;
        return offset;
    }

    /// <summary>
    /// 判断当天是否为休息日：
    /// 在 chinese-days 的 holidays 中（节假日），或周末且不在 workdays 调休上班名单中。
    /// </summary>
    private static bool IsHoliday(DateOnly date)
    {
        var holiday = SettingsStore.Load<ChinaHolidayConfig>(ConfigSections.FileName, ConfigSections.ChinaHoliday);

        // 未同步当年数据时不跳过，避免任务永远不执行
        if (holiday.Year != date.Year || string.IsNullOrWhiteSpace(holiday.Data))
        {
            return false;
        }

        try
        {
            var root = JObject.Parse(holiday.Data);
            var key = date.ToString("yyyy-MM-dd");
            var inWorkdays = root["workdays"]?[key] != null;
            var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            return (root["holidays"]?[key] != null && !inWorkdays) || (isWeekend && !inWorkdays);
        }
        catch
        {
            return false; // 数据解析失败按非节假日处理
        }
    }

    /// <summary>
    /// 计算下一个待执行时刻（含随机偏移），无待执行任务时返回 null。
    /// </summary>
    private DateTime? GetNextTaskTime(TaskConfig taskConfig, TaskScheduleConfig scheduleConfig,
        TimeRangeConfig globalRange, DateOnly today, DateTime now)
    {
        DateTime? next = null;

        foreach (var task in taskConfig.Tasks.Where(t => t.IsEnabled))
        {
            if (!TimeSpan.TryParseExact(task.ExecuteTime, [TimeFormat], CultureInfo.InvariantCulture, out var time))
            {
                continue;
            }

            var scheduledAt = today.ToDateTime(TimeOnly.FromTimeSpan(time));
            if (scheduleConfig.IsNeedRandomTime)
            {
                scheduledAt = scheduledAt.AddMinutes(GetRandomOffset(task.Name, task.TimeRange ?? globalRange));
            }

            if (scheduledAt <= now) continue;
            if (next == null || scheduledAt < next)
            {
                next = scheduledAt;
            }
        }

        return next;
    }

    /// <summary>
    /// 向 adb 真实探测设备是否仍处于 device 状态。
    /// </summary>
    private async Task<bool> IsDeviceAliveAsync(string serial)
    {
        var commandLine = new ArgumentCreator().Append("devices").ToCommandLine();
        var (_, output) = await new CommandExecutor(commandLine).ExecuteWithOutputAsync(_adbPath);

        return output.Any(line =>
        {
            var parts = line.Split('\t');
            return parts.Length == 2
                   && parts[0].Trim() == serial
                   && parts[1].Trim() == "device";
        });
    }

    private void RaiseLog(bool state, string content)
    {
        TaskExecuted?.Invoke(new RuntimeLog
        {
            Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            State = state,
            Content = content
        });
    }
}