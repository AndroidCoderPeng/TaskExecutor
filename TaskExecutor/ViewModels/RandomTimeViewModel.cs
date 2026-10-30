using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskExecutor.Cache;
using TaskExecutor.Services;
using TaskExecutor.Utils;

namespace TaskExecutor.ViewModels;

public partial class RandomTimeViewModel : ObservableObject
{
    #region VM

    [ObservableProperty] private int _lowerLimitTime = 1;
    [ObservableProperty] private int _upperLimitTime = 30;

    #endregion
    
    private readonly IToastService _toastService;
    private readonly TimeRangeConfig _config;

    public RandomTimeViewModel(IToastService toastService)
    {
        _toastService = toastService;

        _config = SettingsStore.Load<TimeRangeConfig>(ConfigSections.FileName, ConfigSections.TimeRange);
        LowerLimitTime = _config.LowerLimitTime;
        UpperLimitTime = _config.UpperLimitTime;
    }

    [RelayCommand]
    private void SaveConfig()
    {
        if (LowerLimitTime is <= 0 or >= 30 || UpperLimitTime is <= 0 or >= 30)
        {
            _toastService.Show("随机等待时间必须在 1 到 30 分钟之间", ToastType.Error);
            return;
        }

        if (LowerLimitTime >= UpperLimitTime)
        {
            _toastService.Show("下限时间不能大于等于上限时间", ToastType.Error);
            return;
        }

        _config.LowerLimitTime = LowerLimitTime;
        _config.UpperLimitTime = UpperLimitTime;
        SettingsStore.Save(ConfigSections.FileName, ConfigSections.TimeRange, _config);
        _toastService.Show("随机等待时间配置已保存", ToastType.Success);
    }
}