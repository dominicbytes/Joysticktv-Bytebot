using JoystickTV.Bot;
using Xunit;

namespace JoystickTV.Bot.Tests;

public sealed class JoystickParserTests
{
    [Fact]
    public void Chat_ExposesBotCommandWithoutCombinedChatRouting()
    {
        var result = JoystickParser.ParseCableMessage("{\"message\":{\"event\":\"ChatMessage\",\"messageId\":\"m1\",\"channelId\":\"c1\",\"text\":\"!scene next\",\"botCommand\":\"scene\",\"botCommandArg\":\"next\",\"author\":{\"username\":\"viewer\",\"displayNameWithFlair\":\"Viewer\",\"isModerator\":true}}}");

        Assert.NotNull(result);
        Assert.Equal(JoystickEventNames.ChatMessage, result!.Name);
        Assert.Equal("scene", result.Arguments["botCommand"]);
        Assert.Equal("next", result.Arguments["botCommandArg"]);
    }

    [Fact]
    public void Tip_MapsTokenFieldsFromStringMetadata()
    {
        var result = JoystickParser.ParseCableMessage("{\"message\":{\"event\":\"StreamEvent\",\"type\":\"Tipped\",\"channelId\":\"c1\",\"metadata\":\"{\\\"who\\\":\\\"viewer\\\",\\\"how_much\\\":25,\\\"tip_menu_item\\\":\\\"Spin\\\"}\"}}");

        Assert.NotNull(result);
        Assert.Equal(JoystickEventNames.Tipped, result!.Name);
        Assert.Equal(25L, result.Arguments["amountTokens"]);
        Assert.Equal("Spin", result.Arguments["tipMenuItem"]);
    }

    [Fact]
    public void UnknownStreamEvent_IsPreservedAsGenericEvent()
    {
        var result = JoystickParser.ParseCableMessage("{\"message\":{\"event\":\"StreamEvent\",\"type\":\"FutureEvent\",\"channelId\":\"c1\",\"client_secret\":\"nope\"}}");

        Assert.NotNull(result);
        Assert.Equal(JoystickEventNames.StreamEvent, result!.Name);
        Assert.Equal("FutureEvent", result.Arguments["streamEventType"]);
        Assert.DoesNotContain("client_secret", (string)result.Arguments["rawJson"]);
    }
}
