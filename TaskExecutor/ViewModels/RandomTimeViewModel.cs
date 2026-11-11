using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskExecutor.Cache;
using TaskExecutor.Services;
using TaskExecutor.Utils;
using TaskExecutor.Enums;

namespace TaskExecutor.ViewModels;

public partial class RandomTimeViewModel : ObservableObject
{
    #region VM

    [ObservableProperty] private int _minLimitTime;
    [ObservableProperty] private int _maxLimitTime = 30;

    #endregion

    private readonly IToastService _toastService;
    private readonly TimeRangeConfig _config;

    public RandomTimeViewModel(IToastService toastService)
    {
        _toastService = toastService;

        _config = SettingsStore.Load<TimeRangeConfig>(ConfigSections.FileName, ConfigSections.TimeRange);
        MinLimitTime = _config.MinLimitTime;
        MaxLimitTime = _config.MaxLimitTime;
    }

    [RelayCommand]
    private void SaveConfig()
    {
        if (MinLimitTime is <= 0 or >= 30 || MaxLimitTime is <= 0 or >= 30)
        {
            _toastService.Show("随机等待时间必须在 1 到 30 分钟之间", ToastType.Error);
            return;
        }

        if (MinLimitTime >= MaxLimitTime)
        {
            _toastService.Show("下限时间不能大于等于上限时间", ToastType.Error);
            return;
        }

        _config.MinLimitTime = MinLimitTime;
        _config.MaxLimitTime = MaxLimitTime;
        SettingsStore.Save(ConfigSections.FileName, ConfigSections.TimeRange, _config);
        _toastService.Show("随机等待时间配置已保存", ToastType.Success);
    }
}