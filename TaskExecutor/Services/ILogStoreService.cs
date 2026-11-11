using TaskExecutor.Model;

namespace TaskExecutor.Services;

public interface ILogStoreService
{
    event Action<RuntimeLog>? Appended;
    void Append(RuntimeLog log);
    IReadOnlyList<RuntimeLog> LoadByDate(DateOnly date);
}