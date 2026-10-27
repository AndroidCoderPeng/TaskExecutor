using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using TaskExecutor.Model;

namespace TaskExecutor.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    #region VM

    [ObservableProperty] private ObservableCollection<MenuItem> _menuItems = [];
    [ObservableProperty] private int _selectedIndex;
    [ObservableProperty] private ObservableObject _currentView;
    [ObservableProperty] private bool _isDeviceConnected;

    #endregion

    public MainWindowViewModel()
    {
        MenuItems =
        [
            new MenuItem { Group = "管理", Icon = "\ue74f", Title = "无线连接" },
            new MenuItem { Group = "管理", Icon = "\ue63f", Title = "任务管理" },
            new MenuItem { Group = "配置", Icon = "\ue629", Title = "导入导出" },
            new MenuItem { Group = "配置", Icon = "\ue622", Title = "目标APP" },
            new MenuItem { Group = "配置", Icon = "\ue690", Title = "邮箱配置" },
            new MenuItem { Group = "配置", Icon = "\ue621", Title = "企业微信配置" },
            new MenuItem { Group = "调度", Icon = "\ue736", Title = "任务等待时间" },
            new MenuItem { Group = "调度", Icon = "\ue625", Title = "任务重置时间" },
            new MenuItem { Group = "调度", Icon = "\ue696", Title = "随机时间范围" },
            new MenuItem { Group = "调度", Icon = "\ue6a6", Title = "调度开关" },
            new MenuItem { Group = "帮助", Icon = "\ue640", Title = "指令测试" },
            new MenuItem { Group = "帮助", Icon = "\ue6c7", Title = "常见问题" },
            new MenuItem { Group = "帮助", Icon = "\ue685", Title = "关于应用" }
        ];

        SelectedIndex = 0;
        CurrentView = App.Services.GetRequiredService<WiFiConnectionViewModel>();
    }
    
    [RelayCommand]
    private void MenuItemSelected(MenuItem menuItem)
    {
        SelectedIndex = MenuItems.IndexOf(menuItem);

        CurrentView = menuItem.Title switch
        {
            "无线连接" => App.Services.GetRequiredService<WiFiConnectionViewModel>(),
            "任务管理" => App.Services.GetRequiredService<TaskManagerViewModel>(),
            "导入导出" => App.Services.GetRequiredService<ImportExportViewModel>(),
            "目标APP" => App.Services.GetRequiredService<TargetAppViewModel>(),
            "邮箱配置" => App.Services.GetRequiredService<EmailConfigViewModel>(),
            "企业微信配置" => App.Services.GetRequiredService<WxConfigViewModel>(),
            "任务等待时间" => App.Services.GetRequiredService<TaskWaitTimeViewModel>(),
            "任务重置时间" => App.Services.GetRequiredService<TaskResetTimeViewModel>(),
            // "随机时间范围" => App.Services.GetRequiredService<RandomTimeRangeViewModel>(),
            // "调度开关" => App.Services.GetRequiredService<ScheduleSwitchViewModel>(),
            // "指令测试" => App.Services.GetRequiredService<CommandTestViewModel>(),
            // "常见问题" => App.Services.GetRequiredService<FaqViewModel>(),
            // "关于应用" => App.Services.GetRequiredService<AboutViewModel>(),
            _ => CurrentView
        };
    }
}