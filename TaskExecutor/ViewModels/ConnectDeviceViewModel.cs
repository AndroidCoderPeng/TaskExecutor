using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TaskExecutor.ViewModels;

public partial class ConnectDeviceViewModel : ObservableObject
{
    #region VM

    [ObservableProperty] private string _deviceName = string.Empty;
    [ObservableProperty] private string _deviceIp = string.Empty;
    [ObservableProperty] private string _connectionState = "未连接";

    #endregion

    [RelayCommand]
    private void RestartAdb()
    {
        
    }
    
    [RelayCommand]
    private void DisconnectDevice()
    {
        
    }
    
    [RelayCommand]
    private void ConnectDevice()
    {
        
    }
}