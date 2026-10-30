using TaskExecutor.Utils;

namespace TaskExecutor.Services;

public interface IToastService
{
    string Message { get; }
    ToastType Type { get; }
    bool IsVisible { get; }
    void Show(string message, ToastType type = ToastType.Info, int durationMs = 2000);
}