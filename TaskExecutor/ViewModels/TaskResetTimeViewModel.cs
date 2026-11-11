using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskExecutor.Cache;
using TaskExecutor.Services;
using TaskExecutor.Utils;

namespace TaskExecutor.ViewModels;

public partial class TaskResetTimeViewModel : ObservableObject
{
    [ObservableProperty] private string _resetTime = "00:00";

    private readonly IToastService _toastService;
    private readonly ResetTimeConfig _config;

    public TaskResetTimeViewModel(IToastService toastService)
    {
        _toastService = toastService;

        _config = SettingsStore.Load<ResetTimeConfig>(ConfigSections.FileName, ConfigSections.ResetTime);
        ResetTime = _config.Time;
    }

    [RelayCommand]
    private void SaveConfig()
    {
        _config.Time = ResetTime;
        SettingsStore.Save(ConfigSections.FileName, ConfigSections.ResetTime, _config);
        _toastService.Show("重置时间配置已保存", ToastType.Success);
    }
}