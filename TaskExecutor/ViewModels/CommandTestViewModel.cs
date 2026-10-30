using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskExecutor.Services;

namespace TaskExecutor.ViewModels;

public partial class CommandTestViewModel : ObservableObject
{
    #region VM

    [ObservableProperty] private string _deviceName = "-";

    #endregion

    private readonly string _adbPath = Path.Combine(AppContext.BaseDirectory, "ADB", "adb.exe");
    private readonly ConnectDeviceViewModel _deviceViewModel;
    private readonly IToastService _toastService;

    public CommandTestViewModel(ConnectDeviceViewModel deviceViewModel, IToastService toastService)
    {
        _deviceViewModel = deviceViewModel;
        _toastService = toastService;
        
        DeviceName = string.IsNullOrEmpty(deviceViewModel.DeviceName) ? "-" : deviceViewModel.DeviceName;
    }

    [RelayCommand]
    private void WakeupPhone()
    {
        
    }
    
    [RelayCommand]
    private void ScreenOffPhone()
    {
        
    }
    
    [RelayCommand]
    private void Screencap()
    {
        
    }
    
    [RelayCommand]
    private void OpenTargetApp()
    {
        
    }
    
    [RelayCommand]
    private void ForceStopApp()
    {
        
    }
}