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

Combat v0.1.5 adds original, cached vector digit/uppercase glyph geometry (no
engine atlas edits), shared with a standalone SVG/HTML review exporter. Large
critical punches and longer angular bursts, elemental vuln/imperil backplates,
measured lane-safe placement and a matching meter header are implemented. The
explicit impact layout preset preserves colors and meter position. Older profiles
receive the custom-lettering toggle without resetting saved layout. Combat 100,
Keyfer 99 and Threads 83 controlled checks passed locally. Live CoreCLR/ImGui
appearance and performance remain an in-game check; the preview is synthetic.

Combat v0.1.6 adds saved Normal/Compact/Ultra-compact display modes with one
chat-observed pack DPS lifecycle (default 4s idle cutoff, event-span DPS, frozen
final result). Locked ultra digits draw only on the existing foreground layer;
unlocked dragging reuses the meter's transparent input target. Normal spacing
and header height are reduced. Reckless is outgoing-only ON/OFF with bounded
pre-hit read-only requested-power samples; unsupported native layouts stay unknown.
Kinetic ready/release/expiry are separate, with state-repeat filtering, a charged
ready graphic and next-weapon-hit correlation that replaces the regular float
instead of adding damage again. All supported floating labels use block lettering;
healing/poison are amplified and acid has local fog/drips. Periodic incoming damage
is separate from direct spell rates. Controlled Combat checks: 126. A private
provided sample was replayed and compared with an independent outgoing-damage
count; no private transcript is checked in. Live drag/click-through, effects and
native power reads still require client validation.

Combat v0.1.7 removes solid floating notice backings and adds neutral-default
per-character Buffs/Debuffs/Skills scales for text and graphics together. Group
origins are explicit for generic utility notices; damage, crits and healing
retain independent controls. Kinetic Ready settles to smaller READY. Notices are
fitted to their available lane. Shared glyph/open-accent geometry was rendered
and visually inspected at 65%, 100% and 135%, with the background grid visible
through all notices. Combat 132 controlled checks passed; live-game background
contrast/overlap remains a client validation. ParserVersion stays 0.1.6.
