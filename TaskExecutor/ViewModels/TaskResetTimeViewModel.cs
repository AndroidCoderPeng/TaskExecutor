using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskExecutor.Cache;
using TaskExecutor.Services;
using TaskExecutor.Enums;
using TaskExecutor.Utils;

namespace TaskExecutor.ViewModels;

public partial class TaskResetTimeViewModel : ObservableObject
{
    [ObservableProperty] private string _resetTime = "00:00";

    private readonly IToastService _toastService;
    private readonly ResetTimeConfig _config;
    private readonly ISchedulerService _schedulerService;

    public TaskResetTimeViewModel(IToastService toastService, ISchedulerService schedulerService)
    {
        _toastService = toastService;
        _schedulerService = schedulerService;

        _config = SettingsStore.Load<ResetTimeConfig>(ConfigSections.FileName, ConfigSections.ResetTime);
        ResetTime = _config.Time;
    }

    [RelayCommand]
    private void SaveConfig()
    {
        if (string.IsNullOrWhiteSpace(ResetTime))
        {
            _toastService.Show("请选择重置时间", ToastType.Warning);
            return;
        }

        _config.Time = ResetTime;
        SettingsStore.Save(ConfigSections.FileName, ConfigSections.ResetTime, _config);
        _toastService.Show("重置时间配置已保存", ToastType.Success);
        _schedulerService.RefreshPlan();
    }
}