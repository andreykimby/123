using CommunityToolkit.Mvvm.Messaging.Messages;

public sealed class CloseWindowMessage : ValueChangedMessage<string>
{
    // payload — произвольная строка: идентификатор окна, reason, "ok"/"cancel" и т.д.
    public CloseWindowMessage(string value = "") : base(value) { }
}
