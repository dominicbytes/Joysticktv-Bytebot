# Installation

## Requirements

- Windows
- Streamer.bot 1.0.4
- .NET Framework 4.8.1
- A Joystick.TV streamer account

## Create a private Joystick.TV bot application

Joysticktv-Bytebot uses credentials from a bot application owned by the same Joystick.TV account that will run the plugin.

1. Sign in to [Joystick.TV](https://joystick.tv/).
2. Open [Bot Applications](https://joystick.tv/applications).
3. Scroll to the bot application section and select **Create Bot** or **Create Bot Application**.
4. Keep the application **Private**. Private is the Joystick.TV default and means only the creating account can install it.
5. Complete the application form using the values below.

| Application field | Value for Joysticktv-Bytebot |
| --- | --- |
| Name | `Joysticktv-Bytebot` |
| Description | `Private Streamer.bot integration for Joystick.TV stream events, chat commands, and messages.` |
| Redirect URL / Redirect URI | `http://127.0.0.1:17994/` |
| Visibility | Private |
| Website | Not required for a private bot |
| Terms of Service | Not required for a private bot |
| Privacy Policy | Not required for a private bot |

The redirect value must match exactly, including `http`, `127.0.0.1`, port `17994`, and the final `/`. The plugin starts a temporary local callback listener at that address while Authorize is running.

### Select permissions

Enable exactly these permissions when creating the application:

| Permission | Why it is required |
| --- | --- |
| `ReceiveStreamEvents` | Receives tips, follows, subscriptions, wheel spins, stream start/end, drop-ins, and other stream events. |
| `ReadMessages` | Receives chat messages so Streamer.bot commands and command arguments can be detected. |
| `SendMessage` | Allows the Streamer.bot Send Message action to post public chat replies, links, and timed messages. |
| `ViewUserPresence` | Receives user-entered and user-left events. |

Do not enable `SendWhisper`, `DeleteMessage`, `BlockUser`, `MuteUser`, `ManageStreamerSettings`, or other permissions. Joystick.TV does not allow an application's permissions to be changed after creation. If the permission selection is wrong, delete the application or create a replacement.

6. Create the application.
7. Copy the generated **OAuth Client ID** and **OAuth Client Secret** to a secure temporary location. Do not place either value in this repository, a Streamer.bot action, a screenshot, or a log.

Joysticktv-Bytebot derives the Basic authorization value internally from the Client ID and Client Secret. Do not manually generate or enter a Basic key.

## Install the plugin

1. Close Streamer.bot.
2. Extract the release ZIP.
3. Copy `dlls/JoystickTV.Bot.dll` and `dlls/StreamerBot.PlatformBridge.Core.dll` into Streamer.bot's `dlls` directory.
4. Start Streamer.bot.
5. Open **Import** and import `JoystickTV.Bot.sb`.
6. Run `[JoystickTV.Bot] Configure`.
7. Complete the configuration window using this mapping:

| Plugin field | What to enter |
| --- | --- |
| Client ID | The application's **OAuth Client ID**. |
| Client secret | The application's **OAuth Client Secret**. |
| Redirect URI | `http://127.0.0.1:17994/` |
| Channel ID | Leave blank initially. This is not the Client ID. |

8. Select **Save**. The client secret is protected with Windows CurrentUser DPAPI before it is written locally.
9. Run `[JoystickTV.Bot] Authorize`.
10. A Joystick.TV authorization page opens in the default browser. Sign in with the account that created the private bot, review the four requested permissions, and approve the installation.
11. Return to Streamer.bot and check the log for:

```text
[JoystickTV.Bot] OAuth authorization completed and tokens were protected with CurrentUser DPAPI.
```

12. Run `[JoystickTV.Bot] Start`.
13. Run `[JoystickTV.Bot] Status` and confirm the log reports `Configured=True`, `Authorized=True`, `Running=True`, and `Connected=True`.
14. Run `[JoystickTV.Bot] Test` to emit synthetic chat and tip events.

The import initializes custom triggers when Streamer.bot starts. It does not automatically open OAuth or start a connection.

## Channel ID

The Channel ID identifies the streamer's Joystick.TV chat channel. It is different from the OAuth Client ID.

The field can normally remain blank during initial configuration. After the plugin receives a Joystick.TV chat, presence, or stream event, it remembers the `channelId` from that connection for outbound messages. Enter a known Channel ID during Configure only when Send Message must work before the first incoming event. A `channelId` is also included in Joysticktv-Bytebot event arguments and can be inspected inside Streamer.bot.

## Verify with live events

1. Start a Joystick.TV stream or use an available Joystick.TV bot testing mechanism.
2. Generate an event such as a tip or chat message.
3. Confirm the corresponding `bridge.joystick.*` custom trigger runs in Streamer.bot.
4. After an incoming event provides the Channel ID, test `[JoystickTV.Bot] Send Message` with a `message` argument.

Joystick.TV chat is processed for commands and diagnostics and is also available to the packaged local combined-chat overlay. The overlay is optional and does not need to be configured for event triggers or Send Message to work.

## Set up combined chat

The release ZIP includes a `combined-chat` directory for displaying Twitch, YouTube, Kick, and Joystick.TV together. Follow [the combined-chat guide](COMBINED-CHAT.md) after the plugin is connected.

## Troubleshooting

### Configure rejects the redirect URI

Use `http://127.0.0.1:17994/` exactly. `https`, another port, a missing final slash, or a non-local address will be rejected.

### The browser reports a redirect mismatch

Confirm that the Redirect URL saved in the Joystick.TV application exactly matches the Redirect URI in Joysticktv-Bytebot. If the Joystick.TV application cannot be edited, create a replacement application with the correct value and permissions.

### Authorization opens but never completes

Confirm that no other program is using local TCP port `17994`, then run Authorize again. Complete the browser approval while the authorization action is waiting.

### Status reports `Configured=True` but `Authorized=False`

Run Authorize and approve the private bot installation. If the client secret changed, run Configure again before Authorize.

### Send Message reports that a Channel ID is unavailable

Allow the plugin to receive one Joystick.TV event first, pass a `channelId` argument to the Send Message action, or enter a previously observed Channel ID through Configure.

## Permissions reference

Joystick.TV's current bot application and OAuth behavior is documented in [Joystick.TV Developer Support](https://support.joystick.tv/developer_support/).

## Files created at runtime

Configuration and OAuth tokens are stored outside the plugin folder under the current Windows user's local application data. Sensitive values are protected with CurrentUser DPAPI. Do not move those files into the repository.
