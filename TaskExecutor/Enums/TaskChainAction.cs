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