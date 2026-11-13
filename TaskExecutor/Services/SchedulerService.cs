using System.IO;
using System.Windows.Threading;
using Newtonsoft.Json;
using TaskExecutor.Cache;
using TaskExecutor.Enums;
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

    // 动作名常量，须与 TaskDialogViewModel.DefaultActions 的动作库名称保持一致
    private const string ActionScreenOn = "亮屏";
    private const string ActionOpenTarget = "打开应用";
    private const string ActionScreenCapture = "截屏";
    private const string ActionSendMessage = "发送消息";
    private const string ActionKillTarget = "关闭应用";
    private const string ActionScreenOff = "息屏";

    // 30s 一次 tick，UI 线程回调，方便直接更新 VM
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly string _adbPath = Path.Combine(AppContext.BaseDirectory, "ADB", "adb.exe");
    private readonly string _screenshotDir = Path.Combine(AppContext.BaseDirectory, "Screenshots");
    private readonly IDeviceStateProvider _deviceState;
    private readonly ILogStoreService _logStore;
    private readonly IAdbHeartbeatService _heartbeatService;
    private readonly IToastService _toastService;
    private readonly ExecutionPlanStore _planStore = new();
    private ExecutionPlan _plan = new(new ExecutionPlanState());
    private bool _planLoaded;
    private bool _planDirty;
    private bool _planErrorReported;

    private bool _isExecuting; // 防重入（串行执行期间跳过 tick）

    private bool _isScheduled;
    private DateTime? _nextTaskTime;
    private DateOnly _statDate = DateOnly.FromDateTime(DateTime.Now);
    private int _todayTotal;
    private int _todaySuccess;
    private SchedulerStatus _currentStatus = new();

    public event Action<SchedulerStatus>? StatusChanged;

    public SchedulerStatus CurrentStatus => _currentStatus;

    public SchedulerService(IDeviceStateProvider deviceState, ILogStoreService logStore,
        IAdbHeartbeatService heartbeatService, IToastService toastService)
    {
        _deviceState = deviceState;
        _logStore = logStore;
        _heartbeatService = heartbeatService;
        _toastService = toastService;
        _timer.Tick += OnTick;
    }

    public void Start()
    {
        if (_isScheduled) return;

        if (!_planLoaded)
        {
            try
            {
                _plan = new ExecutionPlan(_planStore.Load());
                _planLoaded = true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                ReportPlanError(e);
                return;
            }
        }

        RestoreTodayStats(DateOnly.FromDateTime(DateTime.Now));
        _isScheduled = true;
        RefreshPlan();
        _timer.Start();
    }

    /// <summary>
    /// 手动执行一次完整任务链（不走调度计划）。调度 tick 与手动执行共用 _isExecuting 防重入。
    /// </summary>
    public async Task RunOnceAsync(TaskRow task)
    {
        if (_isExecuting)
        {
            _toastService.Show("有任务正在执行，请稍后再试", ToastType.Warning);
            return;
        }

        _isExecuting = true;
        try
        {
            // TODO wait 是用来给目标应用操作时间的停留，与任务链节点间隔无关
            var wait = SettingsStore.Load<WaitTimeConfig>(ConfigSections.FileName, ConfigSections.WaitTime);
            await ExecuteChainAsync(task, wait.Time);
        }
        finally
        {
            _isExecuting = false;
        }
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
            var snapshot = LoadSnapshot();
            var now = DateTime.Now;
            if (!TrySyncPlan(snapshot, now)) return;
            PublishStatus(snapshot);

            if (snapshot.Schedule.IsSkipHoliday && HolidayHelper.IsRestDay(DateOnly.FromDateTime(now))) return;

            while (_isScheduled)
            {
                snapshot = LoadSnapshot();
                now = DateTime.Now;
                if (!TrySyncPlan(snapshot, now)) break;
                if (snapshot.Schedule.IsSkipHoliday && HolidayHelper.IsRestDay(DateOnly.FromDateTime(now))) break;

                var enabled = snapshot.Tasks.Tasks
                    .Where(t => t.IsEnabled)
                    .ToDictionary(t => t.Name, StringComparer.Ordinal);
                var entry = _plan.FindNextDue(now, enabled.Keys.ToHashSet(StringComparer.Ordinal));
                if (entry == null) break;
                var task = enabled[entry.TaskName];

                entry.State = PlannedTaskState.Triggered;
                _planDirty = true;
                if (!TrySavePlan())
                {
                    entry.State = PlannedTaskState.Pending;
                    break;
                }

                PublishStatus(snapshot);
                await ExecuteTaskAsync(task, snapshot.Wait);
            }

            RefreshPlan();
        }
        finally
        {
            _isExecuting = false;
        }
    }

    public void RefreshPlan()
    {
        if (!_planLoaded) return;
        var snapshot = LoadSnapshot();
        if (TrySyncPlan(snapshot, DateTime.Now)) PublishStatus(snapshot);
    }

    private bool TrySyncPlan(ConfigSnapshot snapshot, DateTime now)
    {
        var duplicate = snapshot.Tasks.Tasks
            .GroupBy(t => t.Name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            ReportPlanError(new InvalidDataException($"任务名称「{duplicate.Key}」重复，请先修改配置。"));
            return false;
        }

        var today = DateOnly.FromDateTime(now);
        if (today != _statDate)
        {
            _statDate = today;
            _todayTotal = 0;
            _todaySuccess = 0;
        }

        _planDirty |= _plan.Reconcile(now, snapshot.Tasks, snapshot.Schedule, snapshot.GlobalRange, snapshot.Reset);
        if (!TrySavePlan()) return false;
        _planErrorReported = false;
        return true;
    }

    private bool TrySavePlan()
    {
        if (!_planDirty) return true;
        try
        {
            _planStore.Save(_plan.State);
            _planDirty = false;
            _planErrorReported = false;
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            ReportPlanError(e);
            return false;
        }
    }

    private void ReportPlanError(Exception e)
    {
        Console.WriteLine($"执行计划异常：{e}");
        if (_planErrorReported) return;
        _planErrorReported = true;
        _currentStatus = new SchedulerStatus
        {
            IsScheduled = false,
            NextTaskTime = _currentStatus.NextTaskTime,
            PlannedTasks = _currentStatus.PlannedTasks,
            TodayTotal = _todayTotal,
            TodaySuccess = _todaySuccess
        };
        StatusChanged?.Invoke(_currentStatus);
        _toastService.Show($"执行计划异常，调度已暂停：{e.Message}", ToastType.Error);
    }

    /// <summary>
    /// 按 task.Actions 配置的顺序执行任务链；任一步骤失败立即中断。
    /// 整条链只产生一条日志，与调度执行的统计口径一致。
    /// </summary>
    private async Task ExecuteChainAsync(TaskRow task, int stepInterval)
    {
        var serial = $"{_deviceState.DeviceIp}:5555";
        if (!_deviceState.IsConnected || !await _heartbeatService.ProbeAsync(serial))
        {
            RaiseLog(TaskState.Failed, $"任务「{task.Name}」执行失败：设备离线");
            return;
        }

        if (task.Actions.Count == 0)
        {
            RaiseLog(TaskState.Failed, $"任务「{task.Name}」执行失败：未配置动作链");
            return;
        }

        var adb = new AdbDevice(_adbPath, serial);
        var screenshotPath = ""; // 「截屏」动作的产出，供后续「发送消息」附带
        foreach (var action in task.Actions)
        {
            bool ok;
            switch (action.Name)
            {
                case ActionScreenOn:
                    ok = await adb.ScreenOnAsync();
                    break;
                case ActionOpenTarget:
                    ok = await adb.OpenAppAsync(task.TargetApp.PackageName, task.TargetApp.Activity);
                    break;
                case ActionScreenCapture:
                    screenshotPath = await adb.ScreencapAsync(_screenshotDir) ?? "";
                    ok = screenshotPath != "";
                    break;
                case ActionSendMessage:
                    ok = await SendChainMessageAsync(task, screenshotPath);
                    break;
                case ActionKillTarget:
                    ok = await adb.KillAppAsync(task.TargetApp.PackageName);
                    break;
                case ActionScreenOff:
                    ok = await adb.ScreenOffAsync();
                    break;
                default:
                    ok = false; // 未知动作，fail-fast 并记日志
                    break;
            }

            if (!ok)
            {
                RaiseLog(TaskState.Failed, $"任务「{task.Name}」执行失败：动作「{action.Name}」出错");
                return;
            }
            
            if (stepInterval > 0) await Task.Delay(TimeSpan.FromSeconds(stepInterval));
        }

        RaiseLog(TaskState.Success, $"任务「{task.Name}」执行成功");
    }

    /// <summary>
    /// 执行单个任务：探测设备 → adb 拉起目标 APP → 发送通知 → 等待任务间隔。
    /// </summary>
    private async Task ExecuteTaskAsync(TaskRow task, WaitTimeConfig wait)
    {
        var serial = $"{_deviceState.DeviceIp}:5555";

        // 设备离线：记失败日志并跳过，不反复重试
        if (!_deviceState.IsConnected || !await _heartbeatService.ProbeAsync(serial))
        {
            RaiseLog(TaskState.Failed, $"任务「{task.Name}」执行失败：设备离线");
            return;
        }

        var adb = new AdbDevice(_adbPath, serial);
        var failed = !await adb.OpenAppAsync(task.TargetApp.PackageName, task.TargetApp.Activity);
        RaiseLog(failed ? TaskState.Failed : TaskState.Success, failed
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
        if (task.NotifyMethods.Any(m => m.Name == "QQ 邮箱"))
        {
            try
            {
                await SendMailAsync(content);
            }
            catch (Exception e)
            {
                RaiseLog(TaskState.Failed, $"任务「{task.Name}」邮件通知发送失败：{e.Message}");
            }
        }

        if (task.NotifyMethods.Any(m => m.Name == "企业微信"))
        {
            try
            {
                await SendWechatAsync(content);
            }
            catch (Exception e)
            {
                RaiseLog(TaskState.Failed, $"任务「{task.Name}」企业微信通知发送失败：{e.Message}");
            }
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
    /// 重启后从当天持久化日志恢复今日统计，避免统计随进程重启归零。
    /// </summary>
    private void RestoreTodayStats(DateOnly today)
    {
        // 心跳日志只用于排障，不计入今日任务执行统计
        var logs = _logStore.LoadByDate(today)
            .Where(l => !l.Content.StartsWith(IAdbHeartbeatService.LogPrefix, StringComparison.Ordinal))
            .ToList();

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

        _logStore.Append(log);
        PublishStatus();
    }

    private void PublishStatus(ConfigSnapshot? snapshot = null)
    {
        snapshot ??= LoadSnapshot();
        var enabled = snapshot.Tasks.Tasks
            .Where(t => t.IsEnabled)
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

        var planned = _plan.State.Tasks
            .Where(p => enabled.Contains(p.TaskName))
            .OrderBy(p => p.ScheduledAt)
            .Select(p => new ScheduledTaskInfo(p.TaskName, p.ScheduledAt, p.State))
            .ToArray();

        _nextTaskTime = planned
            .Where(p => p.State == PlannedTaskState.Pending)
            .Select(p => (DateTime?)p.ScheduledAt)
            .FirstOrDefault();

        _currentStatus = new SchedulerStatus
        {
            IsScheduled = _isScheduled,
            NextTaskTime = _nextTaskTime,
            PlannedTasks = Array.AsReadOnly(planned),
            TodayTotal = _todayTotal,
            TodaySuccess = _todaySuccess
        };

        StatusChanged?.Invoke(_currentStatus);
    }
}