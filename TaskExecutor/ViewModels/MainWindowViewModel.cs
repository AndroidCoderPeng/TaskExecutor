using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using TaskExecutor.Cache;
using TaskExecutor.Model;
using TaskExecutor.Services;
using TaskExecutor.Utils;

namespace TaskExecutor.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    #region VM

    [ObservableProperty] private ObservableCollection<MenuItem> _menuItems = [];
    [ObservableProperty] private int _selectedIndex;
    [ObservableProperty] private ObservableObject _currentView;
    [ObservableProperty] private bool _isDeviceConnected;
    [ObservableProperty] private string _deviceName = "-";
    [ObservableProperty] private string _deviceIp = "-";
    [ObservableProperty] private string _heartBeat = "-";
    [ObservableProperty] private bool _isHeartbeatAlive;

    #endregion

    public IToastService Toast { get; }

    // 需要设备连接后才可点击的菜单项
    private readonly MenuItem[] _deviceRequiredItems;

    // 超过 90s 没有成功探测视为停滞（30s 一次探测 + 连续 2 次失败判定）
    private static readonly TimeSpan HeartbeatStaleThreshold = TimeSpan.FromSeconds(90);
    private readonly DispatcherTimer _heartbeatTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime? _lastHeartbeatTime;

    public MainWindowViewModel(IToastService toastService, IAdbHeartbeatService heartbeatService)
    {
        Toast = toastService;

        // 心跳时间变化即刷新；再用 1s 定时器把"秒数"滚起来，界面才看得出心还在跳
        heartbeatService.HeartbeatUpdated += time =>
        {
            _lastHeartbeatTime = time;
            RefreshHeartBeat();
        };
        _lastHeartbeatTime = heartbeatService.LastSuccessTime;
        RefreshHeartBeat();

        _heartbeatTimer.Tick += (_, _) => RefreshHeartBeat();
        _heartbeatTimer.Start();

        var taskManager = new MenuItem { Group = "管理", Icon = "\ue63f", Title = "任务管理" };
        var importExport = new MenuItem { Group = "配置", Icon = "\ue629", Title = "导入导出" };
        var commandTest = new MenuItem { Group = "帮助", Icon = "\ue640", Title = "指令测试" };

        _deviceRequiredItems = [taskManager, importExport, commandTest];

        MenuItems =
        [
            new MenuItem { Group = "管理", Icon = "\ue74f", Title = "连接设备" },
            taskManager,
            new MenuItem { Group = "配置", Icon = "\ue622", Title = "目标APP" },
            importExport,
            new MenuItem { Group = "配置", Icon = "\ue690", Title = "邮箱配置" },
            new MenuItem { Group = "配置", Icon = "\ue621", Title = "企业微信配置" },
            new MenuItem { Group = "调度", Icon = "\ue736", Title = "任务等待时间" },
            new MenuItem { Group = "调度", Icon = "\ue625", Title = "任务重置时间" },
            new MenuItem { Group = "调度", Icon = "\ue696", Title = "随机时间范围" },
            new MenuItem { Group = "调度", Icon = "\ue6a6", Title = "调度开关" },
            commandTest,
            new MenuItem { Group = "帮助", Icon = "\ue6c7", Title = "常见问题" },
            new MenuItem { Group = "帮助", Icon = "\ue685", Title = "关于应用" }
        ];

        // 除受限项外，其余默认可点
        foreach (var item in MenuItems)
        {
            item.IsEnabled = true;
        }

        foreach (var item in _deviceRequiredItems)
        {
            item.IsEnabled = false;
        }

        SelectedIndex = 0;
        CurrentView = App.Services.GetRequiredService<ConnectDeviceViewModel>();

        WeakReferenceMessenger.Default.Register<MessengerMessage.UpdateNavigationStateMessage>(this, (_, msg) =>
        {
            if (msg.Value.IsEnabled)
            {
                // 设备已连接，启用全部导航栏并切换到任务管理
                foreach (var item in _deviceRequiredItems)
                {
                    item.IsEnabled = true;
                }

                SelectedIndex = 1;
                CurrentView = App.Services.GetRequiredService<TaskManagerViewModel>();
            }
            else
            {
                // 设备已断开，禁用部分导航栏（不用切换到设备连接，因为已经在设备连接界面了）
                foreach (var item in _deviceRequiredItems)
                {
                    item.IsEnabled = false;
                }
            }

            IsDeviceConnected = msg.Value.IsEnabled;
            DeviceName = msg.Value.DeviceName;
            DeviceIp = msg.Value.DeviceIp;
        });

        var config = SettingsStore.Load<TargetAppConfig>(ConfigSections.FileName, ConfigSections.TargetApp);
        if (config.Apps.Count == 0)
        {
            config.Apps.Add(new TargetAppRow
            {
                Name = "钉钉",
                PackageName = "com.alibaba.android.rimet",
                Activity = "com.alibaba.android.rimet.biz.LaunchHomeActivity"
            });
            config.Apps.Add(new TargetAppRow
            {
                Name = "企业微信",
                PackageName = "com.tencent.wework",
                Activity = "com.tencent.wework.launch.LaunchSplashActivity"
            });
            config.Apps.Add(new TargetAppRow
            {
                Name = "飞书",
                PackageName = "com.ss.android.lark",
                Activity = "com.ss.android.lark.main.app.MainActivity"
            });
            SettingsStore.Save(ConfigSections.FileName, ConfigSections.TargetApp, config);
        }
    }

    /// <summary>
    /// 把「最近成功心跳时间」换算成界面文本：连接正常时显示 3s / 12s，
    /// 超过阈值显示「无响应」（配合 XAML 里的 StringFormat，最终呈现为「心跳 3s」）。
    /// </summary>
    private void RefreshHeartBeat()
    {
        if (_lastHeartbeatTime is not { } last)
        {
            IsHeartbeatAlive = false;
            HeartBeat = "-";
            return;
        }

        var elapsed = DateTime.Now - last;
        IsHeartbeatAlive = elapsed < HeartbeatStaleThreshold;
        HeartBeat = IsHeartbeatAlive
            ? $"{(int)elapsed.TotalSeconds}s"
            : $"无响应 {(int)elapsed.TotalMinutes}min";
    }

    [RelayCommand]
    private void MenuItemSelected(MenuItem menuItem)
    {
        if (!menuItem.IsEnabled)
        {
            return;
        }

        SelectedIndex = MenuItems.IndexOf(menuItem);

        CurrentView = menuItem.Title switch
        {
            "连接设备" => App.Services.GetRequiredService<ConnectDeviceViewModel>(),
            "任务管理" => App.Services.GetRequiredService<TaskManagerViewModel>(),
            "导入导出" => App.Services.GetRequiredService<ImportExportViewModel>(),
            "目标APP" => App.Services.GetRequiredService<TargetAppViewModel>(),
            "邮箱配置" => App.Services.GetRequiredService<EmailConfigViewModel>(),
            "企业微信配置" => App.Services.GetRequiredService<WxConfigViewModel>(),
            "任务等待时间" => App.Services.GetRequiredService<TaskWaitTimeViewModel>(),
            "任务重置时间" => App.Services.GetRequiredService<TaskResetTimeViewModel>(),
            "随机时间范围" => App.Services.GetRequiredService<RandomTimeViewModel>(),
            "调度开关" => App.Services.GetRequiredService<TaskScheduleViewModel>(),
            "指令测试" => App.Services.GetRequiredService<CommandTestViewModel>(),
            "常见问题" => App.Services.GetRequiredService<FaqViewModel>(),
            "关于应用" => App.Services.GetRequiredService<AboutViewModel>(),
            _ => CurrentView
        };
    }

    partial void OnIsDeviceConnectedChanged(bool value)
    {
        // 只通知这三个 item
        foreach (var item in _deviceRequiredItems)
        {
            item.IsEnabled = value;
        }

        // 断开时若停留在受限页面，退回"无线连接"
        if (!value && MenuItems.ElementAtOrDefault(SelectedIndex) is { } current &&
            _deviceRequiredItems.Contains(current))
        {
            SelectedIndex = 0;
            CurrentView = App.Services.GetRequiredService<ConnectDeviceViewModel>();
        }
    }
}