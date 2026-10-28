namespace TaskExecutor.Cache;

public static class ConfigSections
{
    public const string FileName = "app-config.json";
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

public class TaskConfig
{
    public List<TaskRow> Tasks { get; set; } = [];
    public List<LogRow> Logs { get; set; } = [];
}

public class TaskRow
{
    public string Name { get; set; } = "";
    public TargetAppRow TargetApp { get; set; } = new();
    public string ExecuteTime { get; set; } = "";
    public TimeRangeConfig TimeRange { get; set; } = new();
    public List<string> Actions { get; set; } = [];
    public bool State { get; set; }
}

public class LogRow
{
    public string Time { get; set; } = "";
    public bool State { get; set; }
    public string Content { get; set; } = "";
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

public class TimeRangeConfig
{
    public string LowerLimitTime { get; set; } = "";
    public string UpperLimitTime { get; set; } = "";
}

public class TaskScheduleConfig
{
    public bool IsNeedRandomTime { get; set; } = true;
    public bool IsSkipHoliday { get; set; } = true;
    public bool IsResetDailyTask { get; set; } = true;
}