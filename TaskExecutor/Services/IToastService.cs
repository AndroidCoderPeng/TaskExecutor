using TaskExecutor.Utils;

namespace TaskExecutor.Services;

public interface IToastService
{
    string Message { get; }
    ToastLevel Level { get; }
    bool IsVisible { get; }
    void Show(string message, ToastLevel level = ToastLevel.Info, int durationMs = 2000);
}