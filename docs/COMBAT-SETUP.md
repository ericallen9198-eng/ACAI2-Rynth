# ACAI2 Combat v0.1.0 — standalone Rynth plugin

A separate read-only combat meter and floating text app. Requires managed CoreCLR
RynthCore API 92+ and the engine's matching ImGui bindings. It does not need
Keyfer, Bridge, RynthAi, Threads, Decal, VTank, UtilityBelt or an AI service.
It never casts, attacks, moves your character, changes power/height, operates a
vendor/bank or renders debuff icons. Settings and data are separate from Threads.

## Install

1. Close the game and RynthCore. Extract the entire Windows ZIP into its own
   folder, such as `C:\Games\ACAI2-Combat`. Keep its dependency DLLs together.
2. Reopen RynthCore. In Plugins, add `ACAI2.Rynth.Combat.Plugin.dll` and enable
   it. Do not register the dependency DLLs as plugins. Launch the game.
3. Open with `/acai2combat`. Setup opens the settings; its Preview button shows
   synthetic effects for 30 seconds without changing real statistics.
4. To avoid duplicate floating numbers, enter `/rv plates numbers off`.
   Optionally turn off gain text with `/rv plates gains off`. These commands
   leave Rynth's nameplates and debuff icons enabled. The app does not change
   Rynth settings automatically. Restore either with the command ending in `on`.

## Automatic meter modules

Acid, poison, sneak, summons, thorns, Runic Ward, Second Wind, shield blocks and
kinetic events appear as their supported messages are observed. No class setup
is needed. Setup offers per-module overrides and an Auto button to undo an override.
Discovery indicates evidence from this observation period, not inspection of your
character's trained skills. Sneak percentage is sneak hits / landed direct physical
hits, including zero-damage landed hits; magic, pets, poison, acid and reflections
are excluded. A module can be enabled manually before its first observed event.

The meter shows outgoing DPS and incoming damage/second over the last 60 seconds,
with a denominator ramping from the first damage event to 60 seconds. Totals and
Thread summaries persist independently of that moving rate. Physical defense
combines melee and missile because chat does not consistently identify them.
Evade = evades / (landed physical hits + evades). Resist = resists / (landed magic
hits + resists). Blocks are separate. Average hits include zero-damage landed hits.

Second Wind is counted as a proc, without guessing its unstated heal amount.
Periodic healing has an unspecified source; it is not relabeled Bloodlust or
Aetheria without source evidence. Ward absorption is not healing. Unsupported
messages are ignored rather than assigned invented damage or class mechanics.

## Floating text and art

Colored numbers have adjustable black outlines, bold weight, size, intensity and
alpha. Acid scatters droplets; poison trails wisps; heals produce rising crosses
and rings; crits burst with rays. Rapid same-target ticks coalesce visually while
actual damage totals retain each event. The event queue is bounded to keep busy
fights readable. Subdued mode removes decorative art; floating text can be disabled
without hiding the meter. No debuff icons duplicate RynthVision.

Advanced controls include inward arc, lane gap, overlap, rise, lifetime, screen
anchor and per-kind hue-wheel/alpha controls. The first build uses the engine's
font with size/bold controls, not an external font loader or a second font atlas.
Position/lock, palette and module overrides save per server and character.

**Anchor limitation:** this release uses an adjustable fixed screen anchor.
Reliable camera-follow needs a supported frame-cell/projection API; the plugin
does not guess world coordinates. Place the anchor above your usual character
position. Preview mode is labeled and never enters real totals or saved runs.

## Thread runs, mobs and deaths

A native Thread opening creates a pending record. Realm/instance confirmation
starts it; re-entry resumes that same run. If realm information is unavailable,
Details → Thread runs provides Mark entered / Mark outside controls. Install before
opening a gem for automatic capture. On restore, unfinished records are marked
as gapped and await entry confirmation; no time is invented across reconnects.

The meter defaults to the current Thread when available. New session resets only
session counters and switches the meter to Session; it does not erase Thread data.
Re-enable Use current Thread on meter to switch back. Entry-to-clear wall time
includes time away; Observed inside counts sampled time in the dungeon. Clear
freezes completion statistics; crumble records the later end. Run average DPS is
total attributed damage / observed time. Death and posthumous proc messages are
kept distinct. A killing-blow number is not added again as a damage hit, avoiding
double counting when an attack line already reported it.

Mob breakdown shows names, attributed damage/kills, physical and magic averages,
highest hits, evade and resist rates. Seen is a sample of attackable dynamic IDs
in your current landblock; it can miss short-lived/off-screen/not-yet-cached mobs.
Server kill progress is stored separately from attributed kill notices. Neither
is falsely described as an exact, complete mob census. Detail caps are labeled.

The supplied server line `Sepulchral Archon's attack stops you cold! (Killing
blow: 151 damage. You had 111/462 health.)` records killer, blow, pre-hit health,
maximum and **40 overkill**. The original short death notice is retained. Other
unrecognized killing prose keeps its notice and numeric fields with an unspecified
killer rather than guessing. No ordinary chat or combat-event transcript is saved.

Details → Thread runs contains the Combat app's own compact run history. Automatic
summary transfer into the separate Threads app is not wired in this first build;
neither plugin requires the other. No duplicate gem-research data is created.

## Storage and commands

`%LOCALAPPDATA%\ACAI2-Combat\<hashed server-character>\` contains `ui.json`, an
ownership lock and one compact `run-<id>.json` per observed run. Files hold aggregate
counters and at most 256 named mob rows plus an overflow row and 64 death notices.
UI loads the latest 50 records; files are not automatically deleted. Session-only
totals live in memory unless explicitly exported. No CSV recorder runs. Explicit
exports create compact aggregate JSON in the same folder. Back up this folder
before manually managing records; old ACAI2 histories are not automatically imported.

- `/acai2combat` or `/acai2combat show`: show meter.
- `/acai2combat setup`: settings.
- `/acai2combat hide`: hide meter (floating text has its own toggle).
- `/acai2combat preview`: toggle the 30-second synthetic preview.
- `/acai2combat new`: new in-memory session; preserve runs.
- `/acai2combat entered` / `outside`: explicit run entry controls.
- `/acai2combat export`: export compact summary.
- `/acai2combat status`: version, storage status and dropped-message count.

This build has real-SDK, parser, aggregation, lifecycle, persistence and package
checks. It has not been exercised in a live AC ImGui frame here. First verify
preview, one fight, run entry/clear and reload settings in-game. A display error
hides the panel with a local message; observation and records remain independent.
