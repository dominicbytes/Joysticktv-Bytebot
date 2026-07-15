namespace JoystickTV.Bot;

public static class JoystickEventNames
{
    public const string ChatMessage = "bridge.joystick.chat_message";
    public const string UserEntered = "bridge.joystick.user_entered";
    public const string UserLeft = "bridge.joystick.user_left";
    public const string StreamEvent = "bridge.joystick.stream_event";
    public const string Tipped = "bridge.joystick.tipped";
    public const string WheelSpinClaimed = "bridge.joystick.wheel_spin_claimed";
    public const string Followed = "bridge.joystick.followed";
    public const string Subscribed = "bridge.joystick.subscribed";
    public const string GiftedSubs = "bridge.joystick.gifted_subs";
    public const string DropIn = "bridge.joystick.drop_in";
    public const string StreamStarted = "bridge.joystick.stream_started";
    public const string StreamEnded = "bridge.joystick.stream_ended";

    public static readonly string[] All =
    {
        ChatMessage, UserEntered, UserLeft, StreamEvent, Tipped, WheelSpinClaimed,
        Followed, Subscribed, GiftedSubs, DropIn, StreamStarted, StreamEnded
    };
}
