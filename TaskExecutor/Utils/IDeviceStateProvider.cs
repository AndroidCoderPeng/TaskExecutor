namespace TaskExecutor.Utils;

/// <summary>
/// 设备连接状态的只读视图，供 Service 层使用，避免依赖 ViewModel。
/// </summary>
public interface IDeviceStateProvider
{
    string DeviceIp { get; }
    bool IsConnected { get; }
}