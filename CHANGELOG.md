# Changelog

## [Unreleased]

- Ships the standard Bytebot combined chat (2026.10.31): only a reader scrolling up pauses auto-scroll, so an OBS Browser Source no longer gets stuck partway up after scene switches. Install only one copy.
- The combined chat no longer shows Joystick follower-count updates ("Someone: Follower count updated") after each follow.

## [0.3.0-local.3] - 2026-10-22

- Pre-release test build; no GitHub release. Plugin behavior is unchanged from 0.3.0-local.1.
- Ships the standard Bytebot combined chat (2026.10.22): AltStream incoming raids appear as event rows and highlighted messages as accented chat rows. Install only one copy.

## [0.3.0-local.2] - 2026-10-15

- Pre-release test build; no GitHub release. Plugin behavior is unchanged from 0.3.0-local.1.
- Ships the standard Bytebot combined chat (2026.10.15), identical in all three Bytebot packages; install only one copy.
- Shared Controls accept the AltStream platform filter.

## [0.3.0-local.1] - 2026-10-13

- Pre-release test build; no GitHub release.
- Added access-token gateway authentication and expiry-aware rotating-token refresh.
- Added public OAuth client PKCE while preserving confidential-client configuration.
- Updated OAuth form requests, explicit scopes, v2 events and snake_case gateway actions.
- Preserved existing trigger names and legacy parsing; added v2 chat command and display metadata.
- Added API compatibility tests and a live self-tip testing checklist.

## [0.2.1] - 2026-08-13

- Verified the plugin build and test suite against Streamer.bot 1.0.7.
- Updated the combined-chat overlay for Streamer.bot's current Twitch EventSub emote payload.
- Normalized the shared bridge assembly version so installing both Bytebot plugins is order-independent.

## [0.2.0] - 2026-07-22

- Added a packaged local combined-chat display for Twitch, YouTube, Kick, and Joystick.TV.
- Added Joystick.TV user color, role badge, and structured emote fields to chat events.
- Added public-facing overlay setup and security documentation.
- Preserved `v0.1.0` as the no-chat personal edition.

## [0.1.0] - 2026-07-15

- Initial private preview.
- Added Joystick.TV OAuth, GatewayChannel events, chat command fields, token refresh, reconnect, and Send Message support.
