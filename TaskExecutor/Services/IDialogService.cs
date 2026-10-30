namespace TaskExecutor.Services;

public interface IDialogService
{
    bool? ShowDialog(Type viewType, object? parameter = null);
}