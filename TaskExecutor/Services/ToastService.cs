using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using TaskExecutor.Utils;

namespace TaskExecutor.Services;

public partial class ToastService : ObservableObject, IToastService
{
    #region VM

    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private ToastType _type;
    [ObservableProperty] private bool _isVisible;

    #endregion

    private readonly Dispatcher _dispatcher = Application.Current.Dispatcher;
    private CancellationTokenSource? _cts;

    public void Show(string message, ToastType type = ToastType.Info, int durationMs = 2000)
    {
        _dispatcher.Invoke(() =>
        {
            Message = message;
            Type = type;
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