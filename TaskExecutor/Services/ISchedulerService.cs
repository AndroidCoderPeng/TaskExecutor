using TaskExecutor.Cache;
using TaskExecutor.Utils;

namespace TaskExecutor.Services;

public interface ISchedulerService
{
    /// <summary>
    /// 调度状态快照变化（启停、下次执行时刻、今日统计）。
    /// </summary>
    event Action<SchedulerStatus>? StatusChanged;

    /// <summary>
    /// 任务链执行进度变化（taskName, actionName）；actionName 为空串表示链已开始/结束，无当前节点。
    /// </summary>
    public event Action<string, string>? ChainProgressChanged;
    
    /// <summary>
    /// 当前状态快照，供界面首次进入时初始化。
    /// </summary>
    SchedulerStatus CurrentStatus { get; }

    void Start();

    /// <summary>
    /// 手动触发一次完整的任务链执行（不占用调度计划，与调度 tick 互斥）。
    /// </summary>
    Task RunOnceAsync(TaskRow task);

    void Stop();

    void RefreshPlan();
}