using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskExecutor.Cache;
using TaskExecutor.Services;
using TaskExecutor.Utils;

namespace TaskExecutor.ViewModels;

public partial class TargetAppViewModel : ObservableObject
{
    #region VM

    [ObservableProperty] private ObservableCollection<TargetAppRow> _targetApps;
    [ObservableProperty] private string _appName = string.Empty;
    [ObservableProperty] private string _packageName = string.Empty;
    [ObservableProperty] private string _launcherActivity = string.Empty;

    #endregion

    private readonly IToastService _toastService;
    private readonly TargetAppConfig _config;

    public TargetAppViewModel(IToastService toastService)
    {
        _toastService = toastService;

        _config = SettingsStore.Load<TargetAppConfig>(ConfigSections.FileName, ConfigSections.TargetApp);
        if (_config.Apps.Count == 0)
        {
            _config.Apps.Add(new TargetAppRow
            {
                Name = "钉钉",
                PackageName = "com.alibaba.android.rimet",
                Activity = "com.alibaba.android.rimet.biz.LaunchHomeActivity"
            });
            _config.Apps.Add(new TargetAppRow
            {
                Name = "企业微信",
                PackageName = "com.tencent.wework",
                Activity = "com.tencent.wework.launch.LaunchSplashActivity"
            });
            _config.Apps.Add(new TargetAppRow
            {
                Name = "飞书",
                PackageName = "com.ss.android.lark",
                Activity = "com.ss.android.lark.main.app.MainActivity"
            });
            TargetApps = new ObservableCollection<TargetAppRow>(_config.Apps);
        }

        TargetApps = new ObservableCollection<TargetAppRow>(_config.Apps);
    }

    [RelayCommand]
    private void DeleteTargetApp(TargetAppRow app)
    {
        if (TargetApps.Count == 0) return;
        TargetApps.Remove(app);
        _config.Apps.Remove(app);
        SettingsStore.Save(ConfigSections.FileName, ConfigSections.TargetApp, _config);
    }

    [RelayCommand]
    private void AddTargetApp()
    {
        if (string.IsNullOrWhiteSpace(AppName) || string.IsNullOrWhiteSpace(PackageName) ||
            string.IsNullOrWhiteSpace(LauncherActivity))
        {
            _toastService.Show("请填写完整的应用信息", ToastLevel.Warning);
            return;
        }

        var newApp = new TargetAppRow
        {
            Name = AppName,
            PackageName = PackageName,
            Activity = LauncherActivity
        };
        TargetApps.Add(newApp);
        _config.Apps.Add(newApp);
        SettingsStore.Save(ConfigSections.FileName, ConfigSections.TargetApp, _config);

        // Clear input fields after adding
        AppName = string.Empty;
        PackageName = string.Empty;
        LauncherActivity = string.Empty;
    }
}