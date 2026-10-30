using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskExecutor.Services;
using TaskExecutor.Utils;

namespace TaskExecutor.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    [ObservableProperty] private string _projectHomePage = "https://github.com/AndroidCoderPeng/TaskExecutor";

    private readonly IToastService _toastService;

    public AboutViewModel(IToastService toastService) => _toastService = toastService;

    [RelayCommand]
    private void OpenHomePage()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ProjectHomePage,
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
            _toastService.Show("打开浏览器失败", ToastType.Error);
        }
    }

    [RelayCommand]
    private void CopyLink()
    {
        Clipboard.SetText(ProjectHomePage);
        try
        {
            Clipboard.SetText(ProjectHomePage);
            _toastService.Show("已复制到剪贴板", ToastType.Success);
        }
        catch
        {
            _toastService.Show("复制失败，请手动复制", ToastType.Error); // Clipboard 可能被占用
        }
    }
}