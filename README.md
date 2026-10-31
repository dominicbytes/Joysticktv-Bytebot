# Joysticktv-Bytebot

Joysticktv-Bytebot is a Streamer.bot integration for Joystick.TV OAuth, GatewayChannel events, chat commands, outbound messages, and a local combined-chat display.

> Status: `v0.3.0` pre-release.

## Features

- Joystick.TV authorization-code OAuth with state validation, public-client PKCE and rotating-token refresh
- DPAPI protection for client secrets and OAuth tokens
- Tips/tokens, follows, subscriptions, gifted subscriptions, wheel spins, drop-ins, and stream status events
- Joystick chat command fields exposed to Streamer.bot without adding a second command engine
- Permission-gated Send Message action for replies, links, and Streamer.bot timers
- Standard Bytebot combined-chat page for OBS (Twitch, YouTube, Kick, Rumble, AltStream, optional Joystick.TV); install only one copy across all Bytebot plugins
- Streamer.bot actions for Configure, Authorize, Start, Stop, Reconnect, Status, Send Message, and Test
- Generic preservation of unknown stream events with sanitized `rawJson`

The original `v0.1.0` release remains available as the personal no-chat edition. Starting with `v0.2.0`, Joystick.TV chat can be displayed through the packaged local overlay.

## Install

Download `Joysticktv-Bytebot-v0.3.0.zip` from the repository's Releases page and follow [the installation guide](docs/INSTALLATION.md) and [the upgrade checklist](docs/LOCAL-TESTING.md). Combined-chat setup is covered in [the overlay guide](docs/COMBINED-CHAT.md).

## Security

Every installation uses the owner's own Joystick.TV bot application. Client secrets, access tokens, and refresh tokens must never be placed in repository files, issues, screenshots, or logs. See [Security](SECURITY.md).

## Development

Build instructions are in [docs/BUILDING.md](docs/BUILDING.md). Event names and arguments are documented in [docs/EVENTS.md](docs/EVENTS.md).

## License

MIT License. See [LICENSE](LICENSE).

## Notes

I vibe coded this with GPT 5.6 Sol, 6 Astra, and 6.1 Sol. I have tested it on my own streams and it works. Use at your own risk. 

Be aware that Joystick chat content is routinely 18+ and could violate Twitch, Youtube, and Kick TOS if you include it. It's on you to moderate your chat so you're not banned. 

This was inspired by the KickBot plugin that expanded StreamerBot's abilities to interact with Kick.

## About Dominic Bytes

Greetings! I am Dominic Bytes, the synth walker. I hail from the distant future. Where brains occupy robot bodies, time travel is a trip to the corner store, and the neon glow of our attire is powered by the light of our souls. Join me on a 1.21 gigawatt powered journey of chill vibes with gaming, anime, movies, and more!

- [Website](https://dominicbytes.carrd.co/)
- [X](https://x.com/DominicBytes)
- [Twitch](https://www.twitch.tv/dominicbytes)
- [YouTube](http://www.youtube.com/@DominicBytes)
- [Kick](https://kick.com/dominicbytes)

