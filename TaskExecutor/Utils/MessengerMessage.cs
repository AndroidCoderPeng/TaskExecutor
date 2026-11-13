using CommunityToolkit.Mvvm.Messaging.Messages;

namespace TaskExecutor.Utils;

public class MessengerMessage
{
    public sealed class UpdateNavigationStateMessage(bool enabled, string name, string ip)
        : ValueChangedMessage<(bool IsEnabled, string DeviceName, string DeviceIp)>((enabled, name, ip));

    /// <summary>
    /// 目标应用增删改导致任务集合变化，通知任务管理页重载
    /// </summary>
    public sealed class TasksChangedMessage;
}