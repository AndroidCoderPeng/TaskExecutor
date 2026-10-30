using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskExecutor.Services;

namespace TaskExecutor.ViewModels;

public partial class CreateTaskDialogViewModel : ObservableObject, IDialogParameterReceiver, IDialogCloseRequester
{
    private string _deviceName = string.Empty;
    public event Action<bool> CloseRequested;

    public void OnDialogOpened(object? parameter)
    {
        _deviceName = parameter as string ?? string.Empty;
    }

    [RelayCommand]
    private void CancelDialog()
    {
        CloseRequested.Invoke(false);
    }
}