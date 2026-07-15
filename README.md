# Joysticktv-Bytebot

Joysticktv-Bytebot is a Streamer.bot integration for Joystick.TV OAuth, GatewayChannel events, chat commands, and outbound messages. It does not place JoystickTV into the combined chat.

> Status: `0.1.0` preview for Streamer.bot 1.0.4 on Windows with .NET Framework 4.8.1.

## Features

- Joystick.TV authorization-code OAuth with state validation
- DPAPI protection for client secrets and OAuth tokens
- Tips/tokens, follows, subscriptions, gifted subscriptions, wheel spins, drop-ins, and stream status events
- Joystick chat command fields exposed to Streamer.bot without adding a second command engine
- Permission-gated Send Message action for replies, links, and Streamer.bot timers
- Streamer.bot actions for Configure, Authorize, Start, Stop, Reconnect, Status, Send Message, and Test
- Generic preservation of unknown stream events with sanitized `rawJson`

This integration does not put Joystick.TV chat into Streamer.bot or Rumble-Bytebot combined chat.

## Install

Download `Joysticktv-Bytebot-v0.1.0.zip` from the repository's Releases page and follow [the installation guide](docs/INSTALLATION.md).

## Security

Every installation uses the owner's own Joystick.TV bot application. Client secrets, access tokens, and refresh tokens must never be placed in repository files, issues, screenshots, or logs. See [Security](SECURITY.md).

## Development

Build instructions are in [docs/BUILDING.md](docs/BUILDING.md). Event names and arguments are documented in [docs/EVENTS.md](docs/EVENTS.md).

## License

MIT License. See [LICENSE](LICENSE).

## Notes

I vibe coded this with GPT 5.6 Sol. I have tested it on my own streams and it works. Use at your own risk.

## About Dominic Bytes

Greetings! I am Dominic Bytes, the synth walker. I hail from the distant future. Where brains occupy robot bodies, time travel is a trip to the corner store, and the neon glow of our attire is powered by the light of our souls. Join me on a 1.21 gigawatt powered journey of chill vibes with gaming, anime, movies, and more!

- [Website](https://dominicbytes.carrd.co/)
- [X](https://x.com/DominicBytes)
- [Twitch](https://www.twitch.tv/dominicbytes)
- [YouTube](http://www.youtube.com/@DominicBytes)
- [Kick](https://kick.com/dominicbytes)

