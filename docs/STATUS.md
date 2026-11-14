# Current status — v0.3.1 Keyfer adapter

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

99 controlled checks currently pass, including a real-SDK managed x86 plugin
build. One expected Linux manifest-embedding warning remains: Windows
UpdateResource is required. No Windows in-game loading or live Bridge/AI dispatch
was performed here. See repository Actions for Windows publish results and
[KEYFER-SETUP.md](KEYFER-SETUP.md) for the required attended checks.

The first in-game v0.3.0 check reached the local Keyfer controller but registration
failed with MissingMethodException. Reproduced under an engine-like collectible
assembly loader: System.Net.Http.Json is shared, System.Text.Json is private.
v0.3.1 uses ordinary HTTP with private JSON serialization and adds the isolated
registration/tell regression. No host/runtime replacement is needed for this fix.

## Independent Threads v0.1.0

A separate `ACAI2.Threads` plugin now implements local Thread journaling, recipe
research and user-started native preparation. It has its own package, entry point,
ImGui panel, per-character storage and 36 controlled checks. It has no Keyfer,
Bridge or combat dependency. See [Thread setup](THREADS-SETUP.md) for supported
interfaces, retained-result behavior and the required attended first test.

## Independent Combat v0.1.1

A separate genuine-SDK managed plugin now implements adaptive DPS/defense, proc
readouts, outlined artistic floating text, compact Thread run/mob summaries and
server killing-blow/overkill records. It has no Keyfer/Threads/RynthAi dependency,
no combat actions and no debuff renderer. The first build uses an adjustable
fixed-screen anchor and engine fonts; camera-follow awaits frame-cell API support.
See [Combat setup](COMBAT-SETUP.md). Live ImGui and server validation remain.

Combat v0.1.1 keeps physical/spell defense evidence separate, excludes ambiguous
delivery from both rates, compresses equal float numbers as xN, emphasizes every
recognized critical kind, centers healing above the anchor, and adds short incoming
elemental vuln/imperil notifications. Rynth still owns debuff icons and XP gains.

Combat v0.1.2 adds a bounded big-hit ledger with source/spell and pre-hit native
enchantment context, plus separate incoming/outgoing Reckless percentage and
average/high/low metrics. Unknown/stale/unclassified/layer-ambiguous effect state
is explicit. Lethal peaks and additional death phrasings are retained without
guessing delivery type or re-adding correlated damage. Legacy records have no
retroactively invented debuff snapshot. No game actions or debuff icons are added.

Combat v0.1.4 replaces fractional duplicate text fills with pixel-aligned single
fills and engine-supplied fonts. Critical numbers have smaller captions, angular
impact brackets and shards; healing has a thin halo and side motes. Ordinary
hits stay quieter; saved controls and compact records are preserved. Combat 89,
Keyfer 99 and Threads 83 controlled checks passed; Windows publish verified.
Live appearance needs in-game validation; no font atlas or other plugin changes.
