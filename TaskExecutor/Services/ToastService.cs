using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using TaskExecutor.Utils;

namespace TaskExecutor.Services;

public partial class ToastService : ObservableObject, IToastService
{
    #region VM

    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private ToastLevel _level;
    [ObservableProperty] private bool _isVisible;

    #endregion

    private readonly Dispatcher _dispatcher = Application.Current.Dispatcher;
    private CancellationTokenSource? _cts;

    public void Show(string message, ToastLevel level = ToastLevel.Info, int durationMs = 2000)
    {
        _dispatcher.Invoke(() =>
        {
            Message = message;
            Level = level;
            IsVisible = true;

            _cts?.Cancel(); // 上一条未消失就作废
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _ = HideAfterAsync(durationMs, token);
        });
    }

    private async Task HideAfterAsync(int ms, CancellationToken token)
    {
        try
        {
            await Task.Delay(ms, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _dispatcher.Invoke(() => IsVisible = false);
    }
}