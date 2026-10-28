using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskExecutor.Cache;
using TaskExecutor.Services;
using TaskExecutor.Utils;

namespace TaskExecutor.ViewModels;

public partial class TaskWaitTimeViewModel : ObservableObject
{
    [ObservableProperty] private int _waitTime = 30;

    private readonly IToastService _toastService;
    private readonly WaitTimeConfig _config;
    
    public TaskWaitTimeViewModel(IToastService toastService)
    {
        _toastService = toastService;

        _config = SettingsStore.Load<WaitTimeConfig>(ConfigSections.FileName, ConfigSections.WaitTime);
        WaitTime = _config.Time;
    }
    
    [RelayCommand]
    private void SaveConfig()
    {
        if (WaitTime is <= 0 or >= 300)
        {
            _toastService.Show("等待时间必须在 1 到 300 秒之间", ToastLevel.Error);
            return;
        }
        _config.Time = WaitTime;
        SettingsStore.Save(ConfigSections.FileName, ConfigSections.WaitTime, _config);
        _toastService.Show("等待时间配置已保存", ToastLevel.Success);
    }
}