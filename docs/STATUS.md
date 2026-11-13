# Current status — v0.3.0 Keyfer adapter

User-approved scope: restore Keyfer on Rynth, exclude combat from rollout.

Implemented against the pinned genuine RynthCore SDK:
- Session-scoped local Bridge registration and heartbeat, current identity and
  cancellation; only the configured character enables Keyfer.
- Ported original conversation, arena delivery, emotes, world events, leaderboard
  client and treasure reporting/journal. Bridge v0.17 stays unchanged.
- Local controls/configuration, Rynth-only ownership lock, bounded callback and
  speech queues. Private questions only use the local ask route.
- Native sends only on the pump, through a fixed speech/emote/top/prize catalog.
  No attacks, casts, movement, equipment/loot automation or arbitrary AI commands.
- Combat source retained in Future/test assembly; published plugin excludes it,
  the renderer and ImGui package reference.

98 controlled checks currently pass, including a real-SDK managed x86 plugin
build. One expected Linux manifest-embedding warning remains: Windows
UpdateResource is required. No Windows in-game loading or live Bridge/AI dispatch
was performed here. See repository Actions for Windows publish results and
[KEYFER-SETUP.md](KEYFER-SETUP.md) for the required attended checks.
