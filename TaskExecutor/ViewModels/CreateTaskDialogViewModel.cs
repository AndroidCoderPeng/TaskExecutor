using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskExecutor.Cache;
using TaskExecutor.Services;
using TaskExecutor.Utils;

namespace TaskExecutor.ViewModels;

public partial class CreateTaskDialogViewModel : ObservableObject, IDialogParameterReceiver, IDialogCloseRequester
{
    #region VM

    [ObservableProperty] private string _deviceName = string.Empty;
    [ObservableProperty] private string _systemVersion = string.Empty;
    [ObservableProperty] private string _taskName = string.Empty;
    [ObservableProperty] private string _taskTime = string.Empty;
    [ObservableProperty] private string _tips = string.Empty;
    [ObservableProperty] private bool _isRandomOffsetTime;
    [ObservableProperty] private bool _isNotifyByMail;
    [ObservableProperty] private bool _isNotifyByWechat;

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

    private readonly TaskConfig _config;
    
    public CreateTaskDialogViewModel()
    {
        _config = SettingsStore.Load<TaskConfig>(ConfigSections.FileName, ConfigSections.Task);
    }

    [RelayCommand]
    private void CancelDialog()
    {
        CloseRequested.Invoke(false);
    }

    [RelayCommand]
    private void SaveTask()
    {
        
    }
}