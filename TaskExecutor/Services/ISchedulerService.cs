using TaskExecutor.Model;

namespace TaskExecutor.Services;

public interface ISchedulerService
{
    event Action<RuntimeLog>? TaskExecuted;
    event Action<DateTime?>? NextTaskTimeChanged;
    bool IsScheduled { get; }
    void Start();
    void Stop();
}