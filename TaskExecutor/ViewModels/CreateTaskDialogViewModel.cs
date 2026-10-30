using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskExecutor.Services;

namespace TaskExecutor.ViewModels;

public partial class CreateTaskDialogViewModel : ObservableObject, IDialogParameterReceiver, IDialogCloseRequester
{
    #region VM

    [ObservableProperty] private string _deviceName = string.Empty;
    [ObservableProperty] private string _systemVersion = string.Empty;
    [ObservableProperty] private string _taskName = string.Empty;

    #endregion

    public event Action<bool> CloseRequested;

    public void OnDialogOpened(object? parameter)
    {
        if (parameter is KeyValuePair<string, string> pair)
        {
            DeviceName = pair.Key;
            SystemVersion = pair.Value;
        }
    }

    public CreateTaskDialogViewModel()
    {
    }

    [RelayCommand]
    private void CancelDialog()
    {
        CloseRequested.Invoke(false);
    }
}