# Local API Compatibility Test - 0.3.0-local.3

This is a pre-release test build with no GitHub release. Test Joystick by itself before changing the production stream setup.

## Changes in 0.3.0-local.2 and 0.3.0-local.3

`JoystickTV.Bot.dll` behavior is unchanged from 0.3.0-local.1. These builds only add:

- The standard Bytebot combined chat (version 2026.10.22 in local.3; it adds AltStream incoming raids and highlighted messages), identical across the Rumble, Joystick and AltStream packages. Install ONLY ONE combined chat; see [COMBINED-CHAT.md](COMBINED-CHAT.md).
- AltStream filter support in the shared `StreamerBot.PlatformBridge.Core.dll` used by **[Combined Chat] Controls**.

## Changes from 0.2.1

- Gateway uses the authorized installation's access token.
- OAuth exchanges and refreshes use form POSTs.
- Public OAuth clients use PKCE S256; owner-managed confidential clients remain supported.
- Tokens expire/rotate and are persisted with CurrentUser DPAPI.
- Gateway requests v2 events and sends channel_id. Existing trigger names and v1 parsing remain supported.
- v2 chat commands, color, badges and emotes are normalized.
- ISO timestamps are preserved. Presence and viewer-count notices stay hidden in chat.
- No undocumented outbound tip-menu action was added.

## Test Checklist

1. Close Streamer.bot and install the ZIP's DLLs. For the overlay, install ONLY ONE combined chat: replace the contents of your single existing `combined-chat` folder only if it is older than 2026.10.22. Import the bundle after reviewing existing actions.
2. Keep the public-client checkbox unchecked for your current confidential application, or check it for a new public/PKCE application.
3. Configure, Authorize, Start, then Status. Confirm all four status flags are True.
4. Test a chat message and command. Verify command/argument fields and the overlay.
5. Click one of your own tip-menu items. Check bridge.joystick.tipped; if absent, inspect bridge.joystick.stream_event and sanitized rawJson.
6. Send a message using the existing Send Message action.
7. Stop, Start and Reconnect separately. Confirm one connection and no repeated handlers.
8. Leave the connection running across access-token expiry. Confirm it reconnects and remains authorized. Public tokens are documented as four hours; confidential tokens as ten days.
9. Confirm user-entered/left and viewer-count notices stay hidden while ordinary messages, tips and follows display.

Do not share config.json, tokens, client secrets, full cable URLs or token endpoint requests. A sanitized event fragment and redacted log are sufficient for parser troubleshooting.

## Local Verification

Automated tests cover PKCE, public/confidential form requests, token rotation and ambiguous refresh failure, legacy JWT expiry migration, gateway payloads, v1/v2 trigger mapping, chat metadata and overlay suppression. These fixtures do not establish live API compatibility or the actual self-tip payload.
