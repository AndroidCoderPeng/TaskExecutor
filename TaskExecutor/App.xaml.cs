using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TaskExecutor.Services;
using TaskExecutor.ViewModels;

namespace TaskExecutor;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; }
    
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        
        services.AddSingleton<MainWindowViewModel>();
        
        services.AddSingleton<IToastService, ToastService>();

        services.AddTransient<WiFiConnectionViewModel>();
        services.AddTransient<TaskManagerViewModel>();
        
        services.AddTransient<ImportExportViewModel>();
        services.AddTransient<TargetAppViewModel>();
        services.AddTransient<EmailConfigViewModel>();
        services.AddTransient<WxConfigViewModel>();
        
        services.AddTransient<TaskWaitTimeViewModel>();
        services.AddTransient<TaskResetTimeViewModel>();
        services.AddTransient<RandomTimeViewModel>();
        services.AddTransient<ScheduleSwitchViewModel>();
        
        services.AddTransient<CommandTestViewModel>();
        services.AddTransient<FaqViewModel>();
        services.AddTransient<AboutViewModel>();

        Services = services.BuildServiceProvider();

        var mainWindow = new MainWindow
        {
            DataContext = Services.GetRequiredService<MainWindowViewModel>()
        };

        MainWindow = mainWindow;
        mainWindow.Show();
    }
}