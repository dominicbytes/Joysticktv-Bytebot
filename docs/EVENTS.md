# Streamer.bot Events

Every event includes `platform`, `eventType`, and sanitized event-fragment `rawJson`.

| Event | Code event | Primary arguments |
| --- | --- | --- |
| Chat command/message | `bridge.joystick.chat_message` | `messageId`, `channelId`, `userName`, `displayName`, `message`, `command`, `commandArg`, role flags, `createdAt` |
| User entered | `bridge.joystick.user_entered` | `presenceType`, `userName`, `channelId`, `createdAt` |
| User left | `bridge.joystick.user_left` | `presenceType`, `userName`, `channelId`, `createdAt` |
| Generic stream event | `bridge.joystick.stream_event` | `streamEventType`, `channelId`, `createdAt` plus optional event fields |
| Tip/token | `bridge.joystick.tipped` | `streamEventType`, `channelId`, `userName`, `amountTokens`, `tipMenuItem`, `createdAt` |
| Wheel spin | `bridge.joystick.wheel_spin_claimed` | `streamEventType`, `channelId`, `userName`, `amountTokens`, `prize`, `createdAt` |
| Follow | `bridge.joystick.followed` | `streamEventType`, `channelId`, `userName`, `createdAt` |
| Subscription | `bridge.joystick.subscribed` | `streamEventType`, `channelId`, `userName`, `createdAt` |
| Gifted subscriptions | `bridge.joystick.gifted_subs` | `streamEventType`, `channelId`, `userName`, `count`, `createdAt` |
| Drop-in | `bridge.joystick.drop_in` | `streamEventType`, `channelId`, `userName`, `viewerCount`, `createdAt` |
| Stream started | `bridge.joystick.stream_started` | `streamEventType`, `channelId`, `createdAt` |
| Stream ended | `bridge.joystick.stream_ended` | `streamEventType`, `channelId`, `createdAt` |

Joystick chat events are available to Streamer.bot for commands and diagnostics but are not forwarded to combined chat. `rawJson` never contains OAuth credentials or client secrets.

