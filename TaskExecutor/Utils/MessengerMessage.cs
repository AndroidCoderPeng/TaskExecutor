using CommunityToolkit.Mvvm.Messaging.Messages;

namespace TaskExecutor.Utils;

public class MessengerMessage
{
    public sealed class UpdateNavigationStateMessage(bool enabled) : ValueChangedMessage<bool>(enabled);
}