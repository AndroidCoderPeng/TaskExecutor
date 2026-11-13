using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
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
    [ObservableProperty] private bool _isEditing;

    public string SaveButtonText => IsEditing ? "保存修改" : "添加";

    #endregion

    private readonly string _adbPath = Path.Combine(AppContext.BaseDirectory, "ADB", "adb.exe");
    private readonly ConnectDeviceViewModel _deviceViewModel;
    private readonly IToastService _toastService;
    private readonly ISchedulerService _schedulerService;
    private readonly TargetAppConfig _config;
    private TargetAppRow? _editingApp; // 正在编辑的行
    private string _originalPackageName = string.Empty; // 编辑前的包名，作为任务同步的匹配键

    public TargetAppViewModel(ConnectDeviceViewModel deviceViewModel, IToastService toastService,
        ISchedulerService schedulerService)
    {
        _deviceViewModel = deviceViewModel;
        _toastService = toastService;
        _schedulerService = schedulerService;

        _config = SettingsStore.Load<TargetAppConfig>(ConfigSections.FileName, ConfigSections.TargetApp);
        TargetApps = new ObservableCollection<TargetAppRow>(_config.Apps);
    }

    partial void OnIsEditingChanged(bool value)
    {
        _ = value;
        OnPropertyChanged(nameof(SaveButtonText));
    }

    [RelayCommand]
    private void EditTargetApp(TargetAppRow app)
    {
        IsEditing = true;
        _editingApp = app;
        _originalPackageName = app.PackageName;
        AppName = app.Name;
        PackageName = app.PackageName;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        if (!IsEditing) return;
        ResetEditor();
    }

    [RelayCommand]
    private void DeleteTargetApp(TargetAppRow app)
    {
        var taskConfig = SettingsStore.Load<TaskConfig>(ConfigSections.FileName, ConfigSections.Task);
        var affectedCount = taskConfig.Tasks.Count(t => t.TargetApp.PackageName == app.PackageName);
        if (affectedCount > 0)
        {
            var result = MessageBox.Show(
                $"目标应用「{app.Name}」被 {affectedCount} 个任务引用，删除将同时移除这些任务，确定吗？",
                "删除确认",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.OK) return;

            taskConfig.Tasks.RemoveAll(t => t.TargetApp.PackageName == app.PackageName);
            SettingsStore.Save(ConfigSections.FileName, ConfigSections.Task, taskConfig);
        }

        TargetApps.Remove(app);
        _config.Apps.Remove(app);
        SettingsStore.Save(ConfigSections.FileName, ConfigSections.TargetApp, _config);

        if (affectedCount > 0)
        {
            _schedulerService.RefreshPlan();
            WeakReferenceMessenger.Default.Send(new MessengerMessage.TasksChangedMessage());
            _toastService.Show($"已删除应用「{app.Name}」及 {affectedCount} 个关联任务", ToastType.Success);
        }
    }

    [RelayCommand]
    private async Task AddTargetAppAsync()
    {
        if (string.IsNullOrWhiteSpace(AppName) || string.IsNullOrWhiteSpace(PackageName))
        {
            _toastService.Show("请填写完整的应用信息", ToastType.Warning);
            return;
        }

        // 包名重复校验（编辑时排除自身）
        if (_config.Apps.Any(a => a.PackageName == PackageName && a != _editingApp))
        {
            _toastService.Show("已存在相同包名的应用", ToastType.Warning);
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
            _toastService.Show("获取启动Activity失败，请检查应用", ToastType.Error);
            return;
        }

        if (IsEditing)
        {
            if (_editingApp == null)
            {
                ResetEditor();
                return;
            }
            
            _editingApp.Name = AppName;
            _editingApp.PackageName = PackageName;
            _editingApp.Activity = activity;
            SettingsStore.Save(ConfigSections.FileName, ConfigSections.TargetApp, _config);

            // TargetAppRow 非 INPC，直接改字段不会刷新表格，重建集合触发刷新
            TargetApps = new ObservableCollection<TargetAppRow>(_config.Apps);

            // 按编辑前的包名同步任务快照（支持包名一起改）
            var synced = SyncTaskTargetApps(_originalPackageName, _editingApp);
            _toastService.Show(synced > 0 ? $"应用已更新，已同步 {synced} 个任务" : "应用已更新", ToastType.Success);
            ResetEditor();
        }
        else
        {
            var newApp = new TargetAppRow
            {
                Name = AppName,
                PackageName = PackageName,
                Activity = activity
            };
            TargetApps.Add(newApp);
            _config.Apps.Add(newApp);
            SettingsStore.Save(ConfigSections.FileName, ConfigSections.TargetApp, _config);

            // 新增的应用若与已有任务同包名（孤儿快照），一并修正
            var synced = SyncTaskTargetApps(newApp.PackageName, newApp);
            _toastService.Show(synced > 0 ? $"应用已添加，已同步 {synced} 个任务" : "应用已添加", ToastType.Success);
            
            // 清空编辑器
            AppName = string.Empty;
            PackageName = string.Empty;
        }
    }

    /// <summary>
    /// 把最新应用配置写入所有引用 matchPackageName 的任务快照；
    /// 有变更时持久化任务配置、刷新调度计划并通知任务管理页重载。
    /// </summary>
    private int SyncTaskTargetApps(string matchPackageName, TargetAppRow source)
    {
        var taskConfig = SettingsStore.Load<TaskConfig>(ConfigSections.FileName, ConfigSections.Task);
        var affected = 0;
        foreach (var task in taskConfig.Tasks.Where(t => t.TargetApp.PackageName == matchPackageName))
        {
            task.TargetApp = new TargetAppRow
            {
                Name = source.Name,
                PackageName = source.PackageName,
                Activity = source.Activity
            };
            affected++;
        }
        
        if (affected == 0) return 0;

        SettingsStore.Save(ConfigSections.FileName, ConfigSections.Task, taskConfig);
        _schedulerService.RefreshPlan();
        WeakReferenceMessenger.Default.Send(new MessengerMessage.TasksChangedMessage());
        return affected;
    }
    
    private void ResetEditor()
    {
        IsEditing = false;
        _editingApp = null;
        _originalPackageName = string.Empty;
        AppName = string.Empty;
        PackageName = string.Empty;
    }
}