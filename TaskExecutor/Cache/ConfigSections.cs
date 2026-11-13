using System.Globalization;
using Newtonsoft.Json;
using TaskExecutor.Model;

namespace TaskExecutor.Cache;

public static class ConfigSections
{
    public const string FileName = "app-config.json";
    public const string DeviceIp = "DeviceIp";
    public const string Task = "Task";
    public const string TargetApp = "TargetApp";
    public const string Email = "Email";
    public const string Wechat = "Wechat";
    public const string WaitTime = "WaitTime";
    public const string ResetTime = "ResetTime";
    public const string TimeRange = "TimeRange";
    public const string TaskSchedule = "TaskSchedule";
    public const string ChinaHoliday = "ChinaHoliday";
}

public class DeviceIpConfig
{
    public string Ip { get; set; } = "";
}

public class TaskConfig
{
    public List<TaskRow> Tasks { get; set; } = [];
}

public class TaskRow
{
    public string Name { get; set; } = "";
    public TargetAppRow TargetApp { get; set; } = new();
    public string PlannedAt { get; set; } = "";
    public TimeRangeConfig? TimeRange { get; set; }
    public List<TaskAction> Actions { get; set; } = [];
    public List<string> NotifyMethods { get; set; } = [];
    [JsonIgnore] public string NotifyMethodDisplay => string.Join("、", NotifyMethods);

    public bool IsEnabled { get; set; }

    // 表示序列化或反序列化时忽略这个属性
    [JsonIgnore] public DateTime? ExecuteTime { get; set; }

    [JsonIgnore]
    public string TimeDisplay => ExecuteTime is { } time
        ? time.ToString("HH:mm", CultureInfo.InvariantCulture)
        : "—";
}

public class TargetAppConfig
{
    public List<TargetAppRow> Apps { get; set; } = [];
}

public class TargetAppRow
{
    public string Name { get; set; } = "";
    public string PackageName { get; set; } = "";
    public string Activity { get; set; } = "";
}

public class EmailConfig
{
    public string SmtpServer { get; set; } = "";
    public int SmtpPort { get; set; }
    public string SenderEmail { get; set; } = "";
    public string SenderAuthCode { get; set; } = "";
    public string RecipientEmail { get; set; } = "";
}

public class WechatConfig
{
    public string WebhookKey { get; set; } = "";
    public string MessageTitle { get; set; } = "";
    public bool AttachScreenshot { get; set; } = true;
}

public class WaitTimeConfig
{
    public int Time { get; set; } = 30;
}

public class ResetTimeConfig
{
    public string Time { get; set; } = "00:00";
}

public class TimeRangeConfig
{
    public int MinLimitTime { get; set; }
    public int MaxLimitTime { get; set; } = 30;
}

public class TaskScheduleConfig
{
    public bool IsNeedRandomTime { get; set; } = true;
    public bool IsSkipHoliday { get; set; } = true;
    public bool IsResetDailyTask { get; set; } = true;
}

public class ChinaHolidayConfig
{
    public int Year { get; set; }
    public string LastSyncTime { get; set; } = "";
    public string Data { get; set; } = "";
}