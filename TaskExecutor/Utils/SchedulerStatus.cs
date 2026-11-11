namespace TaskExecutor.Utils;

/// <summary>
/// 调度器状态快照：由 SchedulerService 内部维护，ViewModel 只读。
/// </summary>
public class SchedulerStatus
{
    public bool IsScheduled { get; init; }

    public DateTime? NextTaskTime { get; init; }

    public int TodayTotal { get; init; }

    public int TodaySuccess { get; init; }

    public int SuccessRate => TodayTotal == 0 ? 0 : (int)Math.Round(TodaySuccess * 100.0 / TodayTotal);
}