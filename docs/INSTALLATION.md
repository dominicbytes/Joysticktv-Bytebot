# Installation

## Requirements

- Windows
- Streamer.bot 1.0.4
- .NET Framework 4.8.1
- A Joystick.TV bot application owned by the installing user

## Install the plugin

1. Close Streamer.bot.
2. Extract the release ZIP.
3. Copy `dlls/JoystickTV.Bot.dll` and `dlls/StreamerBot.PlatformBridge.Core.dll` into Streamer.bot's `dlls` directory.
4. Start Streamer.bot.
5. Open **Import** and import `JoystickTV.Bot.sb`.
6. Run `[JoystickTV.Bot] Configure` and enter the private bot application's client ID, client secret, and redirect URI.
7. Run `[JoystickTV.Bot] Authorize` and complete the browser authorization flow.
8. Run `[JoystickTV.Bot] Start`.
9. Run `[JoystickTV.Bot] Status` and verify the connection state.
10. Run `[JoystickTV.Bot] Test` to emit a synthetic event.

The import initializes custom triggers when Streamer.bot starts. It does not automatically open OAuth or start a connection.

## Bot permissions

Request `ReceiveStreamEvents`, `ReadMessages`, `SendMessage`, and `ViewUserPresence`. The current plugin does not require whisper, delete, mute, block, or other moderation permissions.

## Files created at runtime

Configuration and OAuth tokens are stored outside the plugin folder under the current Windows user's local application data. Sensitive values are protected with CurrentUser DPAPI. Do not move those files into the repository.

