using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskExecutor.Cache;
using TaskExecutor.Utils;

namespace TaskExecutor.ViewModels;

public partial class TargetAppViewModel : ObservableObject
{
    #region VM

    [ObservableProperty] private ObservableCollection<TargetAppRow> _targetApps;
    [ObservableProperty] private string _appName;
    [ObservableProperty] private string _packageName;

    #endregion

    private readonly TargetAppConfig _config;

    public TargetAppViewModel()
    {
        _config = SettingsStore.Load<TargetAppConfig>(ConfigSections.FileName, ConfigSections.TargetApp);
        if (_config.Apps.Count == 0)
        {
            _config.Apps.Add(new TargetAppRow
            {
                Name = "钉钉",
                PackageName = "com.alibaba.android.rimet",
                LauncherActivity = ""
            });
            _config.Apps.Add(new TargetAppRow
            {
                Name = "企业微信",
                PackageName = "com.tencent.wework",
                LauncherActivity = ""
            });
            _config.Apps.Add(new TargetAppRow
            {
                Name = "飞书",
                PackageName = "com.ss.android.lark",
                LauncherActivity = ""
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
        var newApp = new TargetAppRow
        {
            Name = AppName,
            PackageName = PackageName,
            LauncherActivity = ""
        };
        TargetApps.Add(newApp);
        _config.Apps.Add(newApp);
        SettingsStore.Save(ConfigSections.FileName, ConfigSections.TargetApp, _config);
    }
}