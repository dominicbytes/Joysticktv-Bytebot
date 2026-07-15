using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using StreamerBot.PlatformBridge.Core;

namespace JoystickTV.Bot;

public static class JoystickParser
{
    public static PlatformEvent? ParseCableMessage(string json)
    {
        var envelope = JObject.Parse(json);
        var type = Text(envelope["type"]);
        if (type == "welcome" || type == "ping" || type == "confirm_subscription" || type == "reject_subscription")
        {
            return null;
        }

        if (!(envelope["message"] is JObject message))
        {
            return null;
        }

        switch (Text(message["event"]))
        {
            case "ChatMessage": return ParseChat(message);
            case "UserPresence": return ParsePresence(message);
            case "StreamEvent": return ParseStreamEvent(message);
            default: return null;
        }
    }

    private static PlatformEvent ParseChat(JObject message)
    {
        var author = message["author"] as JObject ?? new JObject();
        var fields = Base("chat.message", message);
        fields["messageId"] = Text(message["messageId"]);
        fields["channelId"] = Text(message["channelId"]);
        fields["userName"] = Text(author["username"]);
        fields["displayName"] = First(author, "displayNameWithFlair", "displayName", "username");
        fields["message"] = Text(message["text"]);
        fields["command"] = Text(message["botCommand"]);
        fields["commandArg"] = Text(message["botCommandArg"]);
        fields["botCommand"] = fields["command"];
        fields["botCommandArg"] = fields["commandArg"];
        fields["isStreamer"] = Bool(author["isStreamer"]);
        fields["isModerator"] = Bool(author["isModerator"]);
        fields["isSubscriber"] = Bool(author["isSubscriber"]);
        fields["createdAt"] = Text(message["createdAt"]);
        return new PlatformEvent(JoystickEventNames.ChatMessage, fields);
    }

    private static PlatformEvent ParsePresence(JObject message)
    {
        var presenceType = Text(message["type"]);
        var fields = Base("user.presence", message);
        fields["presenceType"] = presenceType;
        fields["userName"] = Text(message["text"]);
        fields["channelId"] = Text(message["channelId"]);
        fields["createdAt"] = Text(message["createdAt"]);
        var name = presenceType.IndexOf("leave", StringComparison.OrdinalIgnoreCase) >= 0 ? JoystickEventNames.UserLeft : JoystickEventNames.UserEntered;
        return new PlatformEvent(name, fields);
    }

    private static PlatformEvent ParseStreamEvent(JObject message)
    {
        var eventType = Text(message["type"]);
        var metadata = Metadata(message["metadata"]);
        var fields = Base("stream.event", message);
        fields["streamEventType"] = eventType;
        fields["channelId"] = Text(message["channelId"]);
        fields["createdAt"] = Text(message["createdAt"]);

        switch (eventType)
        {
            case "Tipped":
                AddUser(fields, metadata);
                fields["amountTokens"] = Number(metadata, "how_much", "howMuch");
                fields["tipMenuItem"] = First(metadata, "tip_menu_item", "tipMenuItem");
                return new PlatformEvent(JoystickEventNames.Tipped, fields);
            case "WheelSpinClaimed":
                AddUser(fields, metadata);
                fields["amountTokens"] = Number(metadata, "how_much", "howMuch");
                fields["prize"] = Text(metadata["prize"]);
                return new PlatformEvent(JoystickEventNames.WheelSpinClaimed, fields);
            case "Followed":
                AddUser(fields, metadata);
                return new PlatformEvent(JoystickEventNames.Followed, fields);
            case "Subscribed":
                AddUser(fields, metadata);
                return new PlatformEvent(JoystickEventNames.Subscribed, fields);
            case "GiftedSubscriptions":
            case "GiftedSubs":
                AddUser(fields, metadata);
                fields["count"] = Number(metadata, "count", "total");
                return new PlatformEvent(JoystickEventNames.GiftedSubs, fields);
            case "StreamDroppedIn":
            case "DropIn":
                AddUser(fields, metadata);
                fields["viewerCount"] = Number(metadata, "viewer_count", "viewers");
                return new PlatformEvent(JoystickEventNames.DropIn, fields);
            case "Started":
            case "StreamResuming":
                return new PlatformEvent(JoystickEventNames.StreamStarted, fields);
            case "Ended":
            case "StreamEnding":
                return new PlatformEvent(JoystickEventNames.StreamEnded, fields);
            default:
                return new PlatformEvent(JoystickEventNames.StreamEvent, fields);
        }
    }

    private static Dictionary<string, object> Base(string eventType, JToken fragment) => new Dictionary<string, object>
    {
        ["platform"] = "joystick",
        ["eventType"] = eventType,
        ["rawJson"] = JsonSanitizer.SanitizeFragment(fragment)
    };

    private static JObject Metadata(JToken? token)
    {
        if (token is JObject obj) return obj;
        if (token?.Type == JTokenType.String)
        {
            try { return JObject.Parse(token.Value<string>() ?? "{}"); } catch (Newtonsoft.Json.JsonException) { }
        }
        return new JObject();
    }

    private static void AddUser(IDictionary<string, object> fields, JObject metadata) => fields["userName"] = First(metadata, "who", "username", "user");
    private static long Number(JObject obj, params string[] names)
    {
        foreach (var name in names) if (obj[name]?.Value<long?>() is long value) return value;
        return 0L;
    }
    private static string First(JObject obj, params string[] names)
    {
        foreach (var name in names) { var value = Text(obj[name]); if (value.Length > 0) return value; }
        return string.Empty;
    }
    private static string Text(JToken? token) => token?.Type == JTokenType.Null ? string.Empty : token?.ToString() ?? string.Empty;
    private static bool Bool(JToken? token) => token?.Value<bool?>() ?? false;
}
