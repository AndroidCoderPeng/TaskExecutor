using TaskExecutor.Model;

namespace TaskExecutor.Services;

public interface ILogStoreService
{
    void Append(RuntimeLog log);
    IReadOnlyList<RuntimeLog> LoadByDate(DateOnly date);
}