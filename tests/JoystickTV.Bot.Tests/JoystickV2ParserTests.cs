using JoystickTV.Bot;
using Newtonsoft.Json.Linq;
using Xunit;

namespace JoystickTV.Bot.Tests;

public sealed class JoystickV2ParserTests
{
    [Theory]
    [InlineData("tipped", JoystickEventNames.Tipped)]
    [InlineData("wheel_spin_claimed", JoystickEventNames.WheelSpinClaimed)]
    [InlineData("followed", JoystickEventNames.Followed)]
    [InlineData("subscribed", JoystickEventNames.Subscribed)]
    [InlineData("gifted_subscriptions", JoystickEventNames.GiftedSubs)]
    [InlineData("stream_dropped_in", JoystickEventNames.DropIn)]
    [InlineData("stream_started", JoystickEventNames.StreamStarted)]
    [InlineData("stream_ending", JoystickEventNames.StreamEnded)]
    [InlineData("enter_stream", JoystickEventNames.UserEntered)]
    [InlineData("leave_stream", JoystickEventNames.UserLeft)]
    [InlineData("future_event", JoystickEventNames.StreamEvent)]
    public void V2Event_PreservesExistingTriggerContract(string type, string expected)
    {
        var result = JoystickParser.ParseCableMessage(Envelope(type, new JObject { ["who"] = "viewer", ["username"] = "viewer" }));
        Assert.Equal(expected, result!.Name);
        Assert.Equal("channel", result.Arguments["channelId"]);
        Assert.Equal("event-id", result.Arguments["eventId"]);
        Assert.Equal(type, result.Arguments["sourceEventType"]);
        Assert.Equal("2026-10-13T12:00:00Z", result.Arguments["createdAt"]);
    }

    [Fact]
    public void V2Chat_PreservesCommandsBadgesColorAndEmotes()
    {
        var data = JObject.Parse("{\"author\":{\"username\":\"viewer\",\"nickname\":\"Viewer Name\",\"color\":\"#12ab34\",\"badges\":{\"mod\":true,\"subscriber\":true}},\"text\":\"!scene next room\",\"emotes\":[{\"name\":\"wave\",\"url\":\"https://example.test/wave.png\"}]}");
        var result = JoystickParser.ParseCableMessage(Envelope("new_message", data))!;
        Assert.Equal(JoystickEventNames.ChatMessage, result.Name);
        Assert.Equal("Viewer Name", result.Arguments["displayName"]);
        Assert.Equal("scene", result.Arguments["botCommand"]);
        Assert.Equal("next room", result.Arguments["botCommandArg"]);
        Assert.Equal("event-id", result.Arguments["messageId"]);
        Assert.Equal("#12ab34", result.Arguments["userColor"]);
        Assert.Equal("[\"moderator\",\"subscriber\"]", result.Arguments["badgesJson"]);
        Assert.Contains("wave.png", (string)result.Arguments["emotesJson"]);
    }

    [Fact]
    public void V2Tip_MapsMenuFieldsAndKeepsSanitizedOriginalPayload()
    {
        var data = new JObject { ["who"] = "streamer", ["how_much"] = 25, ["tip_menu_item"] = "Test item", ["access_token"] = "secret" };
        var result = JoystickParser.ParseCableMessage(Envelope("tipped", data))!;
        Assert.Equal("Tipped", result.Arguments["streamEventType"]);
        Assert.Equal("streamer", result.Arguments["userName"]);
        Assert.Equal(25L, result.Arguments["amountTokens"]);
        Assert.Equal("Test item", result.Arguments["tipMenuItem"]);
        var raw = JObject.Parse((string)result.Arguments["rawJson"]);
        Assert.Equal(2, raw.Value<int>("v"));
        Assert.Null(raw["data"]!["access_token"]);
    }

    [Fact]
    public void V2ViewerCount_PreservesExistingOverlaySuppression()
    {
        var result = JoystickParser.ParseCableMessage(Envelope("viewer_count_updated", new JObject()))!;
        Assert.Equal(JoystickEventNames.StreamEvent, result.Name);
        Assert.Equal("ViewerCountUpdated", result.Arguments["streamEventType"]);
    }

    private static string Envelope(string type, JObject data) =>
        new JObject { ["message"] = new JObject
        {
            ["v"] = 2, ["type"] = type, ["id"] = "event-id", ["channel_id"] = "channel",
            ["occurred_at"] = "2026-10-13T12:00:00Z", ["text"] = "summary", ["data"] = data
        } }.ToString();
}
