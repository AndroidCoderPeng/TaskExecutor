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

    #endregion

    public MainWindowViewModel()
    {
        MenuItems =
        [
            new MenuItem { Group = "管理", Icon = "\ue622", Title = "无线连接" },
            new MenuItem { Group = "管理", Icon = "\ue620", Title = "任务管理" },
            new MenuItem { Group = "配置", Icon = "\ue62a", Title = "导入导出" },
            new MenuItem { Group = "配置", Icon = "\ue685", Title = "目标APP" },
            new MenuItem { Group = "配置", Icon = "\ue685", Title = "邮箱配置" },
            new MenuItem { Group = "配置", Icon = "\ue685", Title = "企业微信配置" },
            new MenuItem { Group = "调度", Icon = "\ue685", Title = "任务等待时间" },
            new MenuItem { Group = "调度", Icon = "\ue685", Title = "任务重置时间" },
            new MenuItem { Group = "调度", Icon = "\ue685", Title = "随机时间范围" },
            new MenuItem { Group = "调度", Icon = "\ue685", Title = "调度开关" },
            new MenuItem { Group = "帮助", Icon = "\ue685", Title = "指令测试" },
            new MenuItem { Group = "帮助", Icon = "\ue685", Title = "常见问题" },
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
            // "应用列表" => App.Services.GetRequiredService<ApplicationsViewModel>(),
            // "提交软件" => App.Services.GetRequiredService<SubmitApplicationViewModel>(),
            // "关于应用" => App.Services.GetRequiredService<AboutTransmitterViewModel>(),
            _ => CurrentView
        };
    }
}