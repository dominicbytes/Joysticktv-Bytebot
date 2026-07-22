# Combined Chat

The packaged static page combines Streamer.bot's Twitch, YouTube, and Kick chat events with `bridge.joystick.chat_message` events from Joysticktv-Bytebot. It does not contact Joystick.TV directly and never receives the OAuth client secret or tokens.

## Streamer.bot servers

1. Open **Servers/Clients > WebSocket Server**.
2. Use address `127.0.0.1`, port `8080`, endpoint `/`, and enable Auto Start.
3. Start the WebSocket server. Authentication must remain disabled for this display-only client.
4. Open **Servers/Clients > HTTP Server**.
5. Use host `127.0.0.1`, port `7474`, and enable Auto Start.
6. Add mapping path `combined-chat` to the extracted release's `combined-chat` directory.
7. Start or restart the HTTP server.

Open `http://127.0.0.1:7474/combined-chat/index.html` and confirm the status reads **Connected**.

Run `[JoystickTV.Bot] Test` and confirm the synthetic `JoystickBotTest` message appears. This verifies the Streamer.bot custom-event and browser-display path without requiring a live Joystick.TV stream.

If the WebSocket server uses another local port or endpoint, append it as an encoded `ws` query parameter. For example:

```text
http://127.0.0.1:7474/combined-chat/index.html?ws=ws%3A%2F%2F127.0.0.1%3A9000%2F
```

## OBS Browser Source

Add a Browser Source using the same local URL. A starting size of 420 by 700 works well. Leave **Shutdown source when not visible** and **Refresh browser when scene becomes active** disabled to preserve the current in-memory rows.

The same URL can be added as an OBS custom browser dock.

## Display behavior

- Twitch, YouTube, and Kick messages use the structured badge and emote information supplied by Streamer.bot when available.
- Joystick.TV streamer, moderator, and subscriber roles render as badge labels.
- Structured Joystick.TV `emotesUsed` assets render only when they provide HTTPS image URLs.
- Chat names and text are always inserted as text, never trusted HTML.
- The display keeps the newest 250 messages in memory and reconnects with bounded backoff.

## Security

Keep both servers bound to `127.0.0.1`. The overlay does not persist a WebSocket password, so Streamer.bot WebSocket authentication is not supported by this version. Do not expose the HTTP or WebSocket server to a public network interface.

The overlay subscribes only to Twitch chat, YouTube messages, Kick chat, and Streamer.bot custom code events. Custom events are filtered to `bridge.joystick.chat_message`.
