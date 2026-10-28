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
        // 严格校验 24 小时制 HH:MM（00:00 ~ 23:59）
        if (!TimeSpan.TryParseExact(ResetTime, @"hh\:mm", null, out var timeSpan)
            || timeSpan >= TimeSpan.FromDays(1))
        {
            _toastService.Show("请输入有效的重置时间（HH:MM，如 08:30）", ToastLevel.Error);
            return;
        }

        _config.Time = timeSpan.ToString(@"hh\:mm");
        SettingsStore.Save(ConfigSections.FileName, ConfigSections.ResetTime, _config);
        _toastService.Show("重置时间配置已保存", ToastLevel.Success);
    }
}