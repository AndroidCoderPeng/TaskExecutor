using CommunityToolkit.Mvvm.Messaging.Messages;

namespace TaskExecutor.Utils;

public class MessengerMessage
{
    public sealed class UpdateNavigationStateMessage(bool enabled, string name, string ip)
        : ValueChangedMessage<(bool IsEnabled, string DeviceName, string DeviceIp)>((enabled, name, ip));
}