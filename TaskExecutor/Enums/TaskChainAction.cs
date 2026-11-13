namespace TaskExecutor.Enums;

/// <summary>
/// 任务链枚举，表示一系列任务动作。
/// </summary>
public enum TaskChainAction
{
    ScreenOn,
    OpenTarget,
    ScreenCapture,
    SendMessage,
    KillTarget,
    ScreenOff,
}

/// <summary>
/// 任务链动作名的唯一事实源：动作库（TaskDialogViewModel）、链执行（SchedulerService）
/// 都从这里取名称/解析，避免字符串在多处硬编码后各自漂移。
/// </summary>
public static class TaskActionNames
{
    public const string ScreenOn = "亮屏";
    public const string OpenTarget = "打开应用";
    public const string ScreenCapture = "截屏";
    public const string SendMessage = "发送消息";
    public const string KillTarget = "关闭应用";
    public const string ScreenOff = "息屏";

    /// <summary>动作名 → 链上动作枚举；未知名称返回 null（执行端 fail-fast）。</summary>
    public static TaskChainAction? FromName(string name) => name switch
    {
        ScreenOn => TaskChainAction.ScreenOn,
        OpenTarget => TaskChainAction.OpenTarget,
        ScreenCapture => TaskChainAction.ScreenCapture,
        SendMessage => TaskChainAction.SendMessage,
        KillTarget => TaskChainAction.KillTarget,
        ScreenOff => TaskChainAction.ScreenOff,
        _ => null
    };
}