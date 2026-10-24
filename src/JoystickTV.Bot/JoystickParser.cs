using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StreamerBot.PlatformBridge.Core;

namespace JoystickTV.Bot;

public static class JoystickParser
{
    public static PlatformEvent? ParseCableMessage(string json)
    {
        using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
        var envelope = JObject.Load(reader);
        var type = Text(envelope["type"]);
        if (type == "welcome" || type == "ping" || type == "confirm_subscription" || type == "reject_subscription")
        {
            return null;
        }

        if (!(envelope["message"] is JObject message))
        {
            return null;
        }

        if (message.Value<int?>("v") == 2) return ParseV2(message);

        switch (Text(message["event"]))
        {
            case "ChatMessage": return ParseChat(message);
            case "UserPresence": return ParsePresence(message);
            case "StreamEvent": return ParseStreamEvent(message);
            default: return null;
        }
    }

    private static PlatformEvent ParseV2(JObject message)
    {
        var type = Text(message["type"]);
        var data = message["data"] as JObject ?? new JObject();
        var normalized = (JObject)data.DeepClone();
        normalized["channelId"] = message["channel_id"];
        normalized["createdAt"] = message["occurred_at"];
        normalized["messageId"] = message["id"];
        PlatformEvent result;
        if (type == "new_message" || type == "chat_message")
        {
            normalized["text"] = data["text"] ?? message["text"];
            normalized["emotesUsed"] = data["emotes"] ?? new JArray();
            var author = normalized["author"] as JObject ?? new JObject();
            var badges = author["badges"] as JObject ?? new JObject();
            author["displayName"] = First(author, "nickname", "username");
            author["usernameColor"] = author["color"];
            author["isStreamer"] = badges["streamer"] ?? false;
            author["isModerator"] = badges["mod"] ?? false;
            author["isSubscriber"] = badges["subscriber"] ?? false;
            normalized["author"] = author;
            var text = Text(normalized["text"]);
            if (text.StartsWith("!", StringComparison.Ordinal))
            {
                var end = 1;
                while (end < text.Length && !char.IsWhiteSpace(text[end])) end++;
                normalized["botCommand"] = text.Substring(1, end - 1);
                normalized["botCommandArg"] = text.Substring(end).TrimStart();
            }
            result = ParseChat(normalized);
        }
        else if (type == "enter_stream" || type == "leave_stream")
        {
            normalized["type"] = type;
            normalized["text"] = data["username"];
            result = ParsePresence(normalized);
        }
        else
        {
            normalized["metadata"] = data;
            normalized["type"] = LegacyStreamType(type);
            result = ParseStreamEvent(normalized);
        }
        result.Arguments["eventId"] = Text(message["id"]);
        result.Arguments["sourceEventType"] = type;
        result.Arguments["rawJson"] = JsonSanitizer.SanitizeFragment(message);
        return result;
    }

    private static string LegacyStreamType(string type)
    {
        switch (type)
        {
            case "tipped": return "Tipped";
            case "wheel_spin_claimed": return "WheelSpinClaimed";
            case "followed": return "Followed";
            case "subscribed": return "Subscribed";
            case "gifted_subscriptions": return "GiftedSubscriptions";
            case "gifted_subs": return "GiftedSubs";
            case "stream_dropped_in": return "StreamDroppedIn";
            case "drop_in": return "DropIn";
            case "stream_started": return "Started";
            case "stream_resuming": return "StreamResuming";
            case "stream_ending": return "StreamEnding";
            case "stream_ended": return "Ended";
            case "viewer_count_updated": return "ViewerCountUpdated";
            default: return type;
        }
    }

    private static PlatformEvent ParseChat(JObject message)
    {
        var author = message["author"] as JObject ?? new JObject();
        var fields = Base("chat.message", message);
        fields["messageId"] = Text(message["messageId"]);
        fields["channelId"] = Text(message["channelId"]);
        fields["userName"] = Text(author["username"]);
        fields["displayName"] = First(author, "displayName", "username", "displayNameWithFlair");
        fields["message"] = Text(message["text"]);
        fields["userColor"] = Text(author["usernameColor"]);
        fields["badgesJson"] = ChatBadges(author).ToString(Newtonsoft.Json.Formatting.None);
        fields["emotesJson"] = JsonSanitizer.SanitizeFragment(message["emotesUsed"] ?? new JArray());
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

    private static JArray ChatBadges(JObject author)
    {
        var badges = new JArray();
        if (Bool(author["isStreamer"])) badges.Add("streamer");
        if (Bool(author["isModerator"])) badges.Add("moderator");
        if (Bool(author["isSubscriber"])) badges.Add("subscriber");
        return badges;
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
