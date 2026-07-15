# Security

## Sensitive values

Never submit a Joystick.TV client secret, access token, refresh token, Basic authorization value, Streamer.bot WebSocket password, configuration file, log containing credentials, or unredacted platform payload.

Joysticktv-Bytebot stores sensitive configuration under the current Windows user profile using Windows DPAPI. A configuration copied to another Windows user should be treated as unusable and replaced through Configure and Authorize.

## Reporting a problem

Because this repository is private, report security problems through a private repository issue. Revoke compromised bot credentials and OAuth tokens through Joystick.TV immediately.

