using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TaskExecutor.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    private const string HomeUrl = "https://github.com/pengxianhong/Pengxh";

    [RelayCommand]
    private void CheckUpdate()
    {
        System.Windows.MessageBox.Show("当前已是最新版本。", "检查更新",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    [RelayCommand]
    private void OpenHome()
    {
        Process.Start(new ProcessStartInfo { FileName = HomeUrl, UseShellExecute = true });
    }

    [RelayCommand]
    private void CopyLink()
    {
        System.Windows.Clipboard.SetText(HomeUrl);
        System.Windows.MessageBox.Show("项目链接已复制到剪贴板", "复制链接",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }
}
