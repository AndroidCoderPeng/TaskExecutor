using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskExecutor.Cache;
using TaskExecutor.Enums;
using TaskExecutor.Services;
using TaskExecutor.Utils;

namespace TaskExecutor.ViewModels;

public partial class TargetAppViewModel : ObservableObject
{
    #region VM

    [ObservableProperty] private ObservableCollection<TargetAppRow> _targetApps;
    [ObservableProperty] private string _appName = string.Empty;
    [ObservableProperty] private string _packageName = string.Empty;

    #endregion

    private readonly string _adbPath = Path.Combine(AppContext.BaseDirectory, "ADB", "adb.exe");
    private readonly ConnectDeviceViewModel _deviceViewModel;
    private readonly IToastService _toastService;
    private readonly TargetAppConfig _config;

    public TargetAppViewModel(ConnectDeviceViewModel deviceViewModel, IToastService toastService)
    {
        _deviceViewModel = deviceViewModel;
        _toastService = toastService;

        _config = SettingsStore.Load<TargetAppConfig>(ConfigSections.FileName, ConfigSections.TargetApp);
        TargetApps = new ObservableCollection<TargetAppRow>(_config.Apps);
    }

    [RelayCommand]
    private void DeleteTargetApp(TargetAppRow app)
    {
        var count = SettingsStore.Load<TaskConfig>(ConfigSections.FileName, ConfigSections.Task)
            .Tasks.Count(row => row.TargetApp.Name == app.Name);
        if (count > 0)
        {
            var result = MessageBox.Show(
                $"目标应用「{app.Name}」仍被 {count} 个任务引用，确定删除吗？",
                "删除确认",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.OK) return;
        }

        TargetApps.Remove(app);
        _config.Apps.Remove(app);
        SettingsStore.Save(ConfigSections.FileName, ConfigSections.TargetApp, _config);
    }

    [RelayCommand]
    private async Task AddTargetAppAsync()
    {
        if (string.IsNullOrWhiteSpace(AppName) || string.IsNullOrWhiteSpace(PackageName))
        {
            _toastService.Show("请填写完整的应用信息", ToastType.Warning);
            return;
        }

        // 获取启动Activity
        if (_deviceViewModel.ConnectionState != "已连接")
        {
            _toastService.Show("设备未连接，请先连接设备", ToastType.Warning);
            return;
        }

        var adb = new AdbDevice(_adbPath, $"{_deviceViewModel.DeviceIp}:5555");
        var activity = await adb.GetLauncherActivityAsync(PackageName);
        if (string.IsNullOrWhiteSpace(activity))
        {
            _toastService.Show("获取启动Activity失败，请检查应用包名", ToastType.Error);
            return;
        }

        var newApp = new TargetAppRow
        {
            Name = AppName,
            PackageName = PackageName,
            Activity = activity
        };
        TargetApps.Add(newApp);
        _config.Apps.Add(newApp);
        SettingsStore.Save(ConfigSections.FileName, ConfigSections.TargetApp, _config);

        // Clear input fields after adding
        AppName = string.Empty;
        PackageName = string.Empty;
    }
}