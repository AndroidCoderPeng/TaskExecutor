using System.Globalization;
using System.IO;
using System.Windows.Threading;
using TaskExecutor.Cache;
using TaskExecutor.Model;
using TaskExecutor.Utils;

namespace TaskExecutor.Services;

public class SchedulerService : ISchedulerService
{
    /// <summary>
    /// 一次 tick 内复用的配置快照，避免重复读盘。
    /// </summary>
    private sealed record ConfigSnapshot(
        ResetTimeConfig Reset,
        TaskScheduleConfig Schedule,
        TaskConfig Tasks,
        TimeRangeConfig GlobalRange,
        WaitTimeConfig Wait);

    private const string TimeFormat = "hh\\:mm";

    // 30s 一次 tick，UI 线程回调，方便直接更新 VM
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly string _adbPath = Path.Combine(AppContext.BaseDirectory, "ADB", "adb.exe");
    private readonly IDeviceStateProvider _deviceState;
    private readonly ILogStoreService _logStore;

    private HashSet<string> _executedKeys = []; // key = $"{taskName}@yyyy-MM-dd"
    private Dictionary<string, int> _randomOffsets = new(); // 每任务当天只抽一次随机偏移(分钟)
    private DateOnly _currentDate;
    private DateOnly _resetAppliedDate; // ResetTime 当天是否已应用过
    private bool _isExecuting; // 防重入（串行执行期间跳过 tick）

    private bool _isScheduled;
    private DateTime? _nextTaskTime;
    private DateOnly _statDate = DateOnly.FromDateTime(DateTime.Now);
    private int _todayTotal;
    private int _todaySuccess;
    private SchedulerStatus _currentStatus = new();

    public event Action<RuntimeLog>? TaskExecuted;
    public event Action<SchedulerStatus>? StatusChanged;

    public SchedulerStatus CurrentStatus => _currentStatus;

    public SchedulerService(IDeviceStateProvider deviceState, ILogStoreService logStore)
    {
        _deviceState = deviceState;
        _logStore = logStore;
        _timer.Tick += OnTick;
    }

    public void Start()
    {
        if (_isScheduled) return;

        _currentDate = DateOnly.FromDateTime(DateTime.Now);
        RestoreTodayStats(_currentDate); // 重启后从持久化日志恢复今日统计
        _isScheduled = true;
        _timer.Start();
        PublishStatus();
    }

    public void Stop()
    {
        _isScheduled = false;
        _timer.Stop();
        PublishStatus();
    }

    private async void OnTick(object? s, EventArgs e)
    {
        if (_isExecuting) return;

        _isExecuting = true;
        try
        {
            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);

            // 一次 tick 只加载一次配置：新建/编辑/启停任务无需重启即生效
            var snapshot = LoadSnapshot();

            ResetIfNeeded(today, now, snapshot);

            if (snapshot.Schedule.IsSkipHoliday && HolidayHelper.IsRestDay(today))
            {
                return;
            }

            // 收集今天已到点且未执行的任务
            var dueTasks = CollectDueTasks(snapshot, today, now);

            // 按实际触发时刻排序，串行执行（任务之间隔 WaitTime 秒）
            foreach (var task in dueTasks)
            {
                await ExecuteTaskAsync(task, snapshot.Wait);
            }

            _nextTaskTime = GetNextTaskTime(snapshot, today, now);
            PublishStatus();
        }
        finally
        {
            _isExecuting = false;
        }
    }

    /// <summary>
    /// 跨天 / ResetTime 到点时的状态重置。
    /// </summary>
    private void ResetIfNeeded(DateOnly today, DateTime now, ConfigSnapshot snapshot)
    {
        // 跨天重置：清空已执行标记、重抽随机偏移、统计归零
        if (today != _currentDate)
        {
            _currentDate = today;
            _executedKeys = [];
            _randomOffsets = [];
            _resetAppliedDate = default;

            _statDate = today;
            _todayTotal = 0;
            _todaySuccess = 0;
            return;
        }

        if (_resetAppliedDate == today) return;
        if (!TimeSpan.TryParseExact(snapshot.Reset.Time, [TimeFormat], CultureInfo.InvariantCulture,
                out var resetTime)) return;
        if (now.TimeOfDay < resetTime) return;

        // ResetTime 到点重置（IsResetDailyTask 开启时才清空，实现当日可二次运行）
        _resetAppliedDate = today;
        if (snapshot.Schedule.IsResetDailyTask)
        {
            _executedKeys = [];
            _randomOffsets = [];
        }
    }

    /// <summary>
    /// 收集今天已到点且未执行过的任务（按触发时刻升序）。
    /// </summary>
    private List<TaskRow> CollectDueTasks(ConfigSnapshot snapshot, DateOnly today, DateTime now)
    {
        var dueTasks = new List<(TaskRow Task, DateTime ScheduledAt)>();

        foreach (var task in snapshot.Tasks.Tasks.Where(t => t.IsEnabled))
        {
            if (!TimeSpan.TryParseExact(task.ExecuteTime, [TimeFormat], CultureInfo.InvariantCulture, out var time))
            {
                continue;
            }

            var scheduledAt = today.ToDateTime(TimeOnly.FromTimeSpan(time));
            if (snapshot.Schedule.IsNeedRandomTime)
            {
                scheduledAt =
                    scheduledAt.AddMinutes(GetRandomOffset(task.Name, task.TimeRange ?? snapshot.GlobalRange));
            }

            if (scheduledAt > now) continue;
            // Add 返回 false 说明 key 已存在 => 今天已执行过
            if (!_executedKeys.Add($"{task.Name}@{today:yyyy-MM-dd}")) continue;

            dueTasks.Add((task, scheduledAt));
        }

        return dueTasks.OrderBy(x => x.ScheduledAt).Select(x => x.Task).ToList();
    }

    /// <summary>
    /// 执行单个任务：探测设备 → adb 拉起目标 APP → 发送通知 → 等待任务间隔。
    /// </summary>
    private async Task ExecuteTaskAsync(TaskRow task, WaitTimeConfig wait)
    {
        var serial = $"{_deviceState.DeviceIp}:5555";

        // 设备离线：记失败日志并跳过，不反复重试
        if (!_deviceState.IsConnected || !await IsDeviceAliveAsync(serial))
        {
            RaiseLog(TaskState.Failed, $"任务「{task.Name}」执行失败：设备离线");
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
        RaiseLog(failed ? TaskState.Failed : TaskState.Success,
            failed
                ? $"任务「{task.Name}」执行失败：启动 {task.TargetApp.Name} 失败"
                : $"任务「{task.Name}」执行成功：已启动 {task.TargetApp.Name}");

        if (!failed)
        {
            await NotifyAsync(task, $"任务「{task.Name}」执行成功");
        }

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
            RaiseLog(TaskState.Failed, $"任务「{task.Name}」{task.NotifyMethod}通知发送失败：{e.Message}");
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
    /// 计算下一个待执行时刻（含随机偏移），无待执行任务时返回 null。
    /// </summary>
    private DateTime? GetNextTaskTime(ConfigSnapshot snapshot, DateOnly today, DateTime now)
    {
        DateTime? next = null;

        foreach (var task in snapshot.Tasks.Tasks.Where(t => t.IsEnabled))
        {
            if (!TimeSpan.TryParseExact(task.ExecuteTime, [TimeFormat], CultureInfo.InvariantCulture, out var time))
            {
                continue;
            }

            var scheduledAt = today.ToDateTime(TimeOnly.FromTimeSpan(time));
            if (snapshot.Schedule.IsNeedRandomTime)
            {
                scheduledAt =
                    scheduledAt.AddMinutes(GetRandomOffset(task.Name, task.TimeRange ?? snapshot.GlobalRange));
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

    /// <summary>
    /// 重启后从当天持久化日志恢复今日统计，避免统计随进程重启归零。
    /// </summary>
    private void RestoreTodayStats(DateOnly today)
    {
        var logs = _logStore.LoadByDate(today);
        _statDate = today;
        _todayTotal = logs.Count;
        _todaySuccess = logs.Count(l => l.State == TaskState.Success);
    }

    private static ConfigSnapshot LoadSnapshot() => new(
        SettingsStore.Load<ResetTimeConfig>(ConfigSections.FileName, ConfigSections.ResetTime),
        SettingsStore.Load<TaskScheduleConfig>(ConfigSections.FileName, ConfigSections.TaskSchedule),
        SettingsStore.Load<TaskConfig>(ConfigSections.FileName, ConfigSections.Task),
        SettingsStore.Load<TimeRangeConfig>(ConfigSections.FileName, ConfigSections.TimeRange),
        SettingsStore.Load<WaitTimeConfig>(ConfigSections.FileName, ConfigSections.WaitTime));

    private void RaiseLog(TaskState state, string content)
    {
        var log = new RuntimeLog
        {
            Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            State = state,
            Content = content
        };

        // 统计口径：每条日志算一次执行，仅 Success 计入成功数
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (today != _statDate)
        {
            _statDate = today;
            _todayTotal = 0;
            _todaySuccess = 0;
        }

        _todayTotal++;
        if (state == TaskState.Success)
        {
            _todaySuccess++;
        }

        _logStore.Append(log); // 内部已容错，落盘失败不影响调度
        TaskExecuted?.Invoke(log);
        PublishStatus();
    }

    private void PublishStatus()
    {
        _currentStatus = new SchedulerStatus
        {
            IsScheduled = _isScheduled,
            NextTaskTime = _nextTaskTime,
            TodayTotal = _todayTotal,
            TodaySuccess = _todaySuccess
        };

        StatusChanged?.Invoke(_currentStatus);
    }
}