using TaskExecutor.Model;
using TaskExecutor.Utils;

namespace TaskExecutor.Services;

public interface ISchedulerService
{
    /// <summary>
    /// 单条任务执行日志，供界面追加日志行。
    /// </summary>
    event Action<RuntimeLog>? TaskExecuted;

    /// <summary>
    /// 调度状态快照变化（启停、下次执行时刻、今日统计）。
    /// </summary>
    event Action<SchedulerStatus>? StatusChanged;

    /// <summary>
    /// 当前状态快照，供界面首次进入时初始化。
    /// </summary>
    SchedulerStatus CurrentStatus { get; }

    void Start();

    void Stop();
}