using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Skarynagram.Features.InstaViewer.Messages;

internal sealed class ClearClientMediaMessage : ValueChangedMessage<string>
{
    public ClearClientMediaMessage(string reason) : base(reason) { }
}
