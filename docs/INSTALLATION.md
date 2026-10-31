# Installation

## Local Test Build

This 0.3.0-local.3 build is for testing before publication. It adds access-token gateway authentication, rotating refresh tokens, public-client PKCE, and v2 events. Existing bridge.joystick.* trigger names and legacy event parsing are preserved.

Requirements: Windows, Streamer.bot 1.0.7, .NET Framework 4.8.1, and a Joystick.TV streamer account.

## Choose Your OAuth Client

Existing installations may keep their own confidential bot application and client secret. New desktop installations can use a public OAuth client with PKCE and no secret.

OAuth client type and marketplace visibility are different settings. A public OAuth client can still be a private bot that only its owner installs.

1. Sign in to [Joystick.TV](https://joystick.tv/).
2. Open [Bot Applications](https://joystick.tv/applications) and create or inspect your bot application.
3. Keep marketplace visibility Private for an owner-only installation.
4. For a new desktop application, select the public/PKCE OAuth client type if offered.
5. Register the redirect URI `http://127.0.0.1:17994/`.
6. Grant the following documented scopes:

| Scope | Purpose |
| --- | --- |
| `chat:read` | Receive messages and expose commands to Streamer.bot |
| `chat:write` | Send public messages |
| `stream:read` | Receive stream events and read stream state |

Older application screens may show ReceiveStreamEvents, ReadMessages, SendMessage and ViewUserPresence. The plugin now requests the modern scope string above. If the server refuses a scope, check the application's grant settings before authorizing again.

7. Record the Client ID. Confidential clients also have a Client Secret; public clients do not.
8. Keep credentials out of source files, action definitions, screenshots and logs.

The plugin uses Basic authentication only at confidential OAuth token exchange/refresh. Gateway connections use the OAuth access token for both client types.

## Install or Update

1. Close Streamer.bot.
2. Extract the local test ZIP.
3. Replace the two files in Streamer.bot's `dlls` directory with `JoystickTV.Bot.dll` and `StreamerBot.PlatformBridge.Core.dll` from the ZIP.
4. If you use the overlay, install ONLY ONE combined chat. Every Bytebot package ships the same `combined-chat` folder. Replace the contents of your single existing folder only if this package's chat version (2026.10.22) is newer, and never map a second copy. See [COMBINED-CHAT.md](COMBINED-CHAT.md).
5. Start Streamer.bot and import `JoystickTV.Bot.sb`. Review the import so your custom actions are preserved.
6. Run `[JoystickTV.Bot] Configure` and check the fields below.

| Plugin field | Value |
| --- | --- |
| Public OAuth client (PKCE, no secret) | Check only for a public OAuth client. Leave unchecked for your existing confidential application. |
| Client ID | Your application's Client ID |
| Client secret | Confidential clients: enter the secret, or leave blank to keep the existing one. Public clients: unused. |
| Redirect URI | `http://127.0.0.1:17994/` |
| Channel ID | Optional. Leave blank to learn it from an incoming event. |

7. Save. Configure stops the running connection; changing the client, client type, secret or redirect clears old tokens.
8. Run `[JoystickTV.Bot] Authorize`, approve the requested scopes in your browser, and wait for the successful authorization log.
9. Run `[JoystickTV.Bot] Start`.
10. Run `[JoystickTV.Bot] Status`. Expect Configured=True, Authorized=True, Running=True and Connected=True after the gateway subscription is confirmed.
11. Run `[JoystickTV.Bot] Test` to verify synthetic chat and tip triggers.
12. Refresh OBS browser sources/docks after updating overlay files.

Authorize stops the gateway while replacing credentials. Run Start again after authorization completes. Import does not automatically start OAuth or connect to Joystick.

Tokens and client secrets remain protected with Windows CurrentUser DPAPI. Runtime files are under `%LOCALAPPDATA%\\StreamerBot.PlatformBridge\\JoystickTV.Bot`, outside the plugin folder. Do not put them into a repository.

## Test the Website Tip-Menu Feature

Joystick's October 13, 2026 announcement says a streamer can click their own menu items to generate a tip-style chat notification without spending tokens or advancing tip goals. It does not announce an outbound bot action.

1. Attach a temporary diagnostic action to `bridge.joystick.tipped`.
2. Click one of your own tip-menu items on Joystick.TV.
3. Check `tipMenuItem`, `amountTokens`, `userName`, `sourceEventType` and the action firing.
4. If the tip trigger does not run, check `bridge.joystick.stream_event` and its sanitized `rawJson` for the actual event type.
5. Also test a real chat command, follow/subscription if available, and Send Message with a `message` argument.

The announcement does not specify the gateway payload. Live confirmation is still required; synthetic tests are not proof of real self-tip behavior.

## Refresh and Reconnect

The plugin refreshes an expiring token before reconnecting and persists the newest rotated access/refresh pair. It also reconnects before token expiry during long sessions.

Refresh tokens are single-use. After a failed or ambiguous refresh, the plugin discards the potentially consumed pair and requires Authorize again. It does not retry an old refresh token. Run Authorize if an existing installation's tokens have expired.

## Troubleshooting

- **Public client has no secret:** Check the public OAuth client checkbox; do not generate a Basic key.
- **Redirect mismatch:** Check the app's registered redirect and the plugin value. Use an HTTP loopback address ending in a slash. The default is `http://127.0.0.1:17994/`.
- **Authorization times out:** Check that port 17994 is free, and complete browser approval within five minutes.
- **Subscription rejected:** Check the bot's granted scopes and authorize again.
- **Token refresh fails:** Run Authorize again, then Start. Avoid restoring or reusing an older refresh token.
- **No Channel ID:** Receive an event first, configure an observed Channel ID, or pass `channelId` to Send Message.
- **Controls say disconnected:** Wait for subscription confirmation, then inspect the redacted plugin logs.

Joystick presence, viewer-count and follower-count events remain available for actions but are hidden from the combined-chat display. The optional overlay is documented in [COMBINED-CHAT.md](COMBINED-CHAT.md).

## API Reference

[Joystick.TV Developer Support](https://support.joystick.tv/developer_support/) documents scopes, PKCE, token rotation, gateway v2 and deprecated camelCase action keys.
