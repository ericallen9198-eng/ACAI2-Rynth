# ACAI2 Combat v0.1.4 — standalone Rynth plugin

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
   Keep floating XP/Luminance with `/rv plates gains on`. These commands
   leave Rynth's nameplates and debuff icons enabled. The app does not change
   Rynth settings automatically. Restore either with the command ending in `on`.

## v0.1.4 floating text redesign

Incoming stays left, outgoing right, and healing rises above the center anchor.
Regular hits are quiet; crits use a large number with a smaller CRIT caption,
angular impact brackets and brief shards outside the lettering. Incoming crits
use coral danger accents; acid/poison crits keep their elemental colors. Healing
uses a thin halo and rising side motes. Vuln/imperil labels retain elemental colors.
Subdued effects disables decoration; existing size, color and emphasis controls
and saved settings remain available.

Text uses engine-supplied fonts, rounded pixel positions, integer outline offsets
and one fill pass, removing the previous fractional duplicate bold fill. This
reduces a source of blur; the engine's baked font resolution and selected font
size still affect sharpness. No engine font atlas is modified. The concept images
are style references, not a pixel-exact promise or screenshots of this build.
Use Setup → Preview effects to check both directions and healing without changing
statistics. Live rendering still needs verification in your game.

## v0.1.2 big hits and Reckless module

Details → Thread runs → select a run → Big hits opens saved snapshots. Its
Reckless IN/OUT summary is shown with that run. ParserVersion remains 0.1.2 for
this UI-only patch; the parsing/context format is unchanged.

Details → Big hits keeps the **24 largest incoming hit snapshots** for the session
and each tracked Thread. Default minimum is 100 reported damage; adjust Big hit
minimum in Setup. Killing-blow notices always qualify for ranking and remain in
the separate bounded death history even if they fall outside the top 24.

Each snapshot records time, attacker, damage, physical/spell/unknown delivery,
element, named spell when present, critical and Reckless markers, and effect
context. A matched preceding hit is upgraded to lethal by name/damage within one
second; the evidence explicitly describes that correlation. Unmatched killing
blows stay unclassified rather than entering the physical or magic denominator.
The overall peak includes server death-blow numbers, including legacy records,
without blindly adding a killing blow to already-counted damage.

The context comes from native player-enchantment samples about every 100ms and
confirmed enchantment changes. Only samples/events at or before the hit may be
used. Samples older than 500ms, missing/truncated registry data, observation gaps,
layer-ambiguous changes and unconfirmed cast changes are labeled unknown/uncertain.
Snapshot age, unclassified custom spell IDs and uncertainty are visible. A recent
sample describes last observed client state, not a guarantee of exact server-tick
state. Retail Imperil/vuln IDs come from embedded reference metadata, requiring no
RynthAi plugin or identification actions. Native expiration is used; no duration
is invented from a spell's nominal base duration. Rynth still owns debuff icons.

Reckless discovers itself from explicit Reckless!/Recklessness! hit prefixes.
Incoming and outgoing rows separately show tagged physical-hit percentage,
average, high and low, including zero-damage landed hits and tagged crits.
Spells, procs, summons and evades do not enter these physical-hit denominators.
The prefix is preserved as hit metadata and removed from the mob name. A marker
does not establish which participant owns the skill or prove a damage multiplier.
Old summaries cannot recover missing low values or at-hit effects. Start a new
Thread/session for clean new-module percentages; legacy totals remain labeled.

Big-hit context persists with tracked Thread records, with the existing five-second
save and logout flush. Session-only hits outside a tracked Thread remain in memory
unless exported, as before. No ordinary combat transcript or full enchantment
history is written. Old death names can be interpreted from the preserved text;
historical debuff states remain unknown. Damage is the server-reported hit amount,
not an invented pre-armor/pre-Ward value; ward absorption stays separate.

## v0.1.1 display and defense changes

Incoming damage/effects scroll left; outgoing attacks/procs scroll right. Healing
rises above the center anchor, with a Healing height slider to position it over
your character's head. This remains a fixed screen anchor, not world tracking.
Quiet hit size/opacity soften normal hits. Critical emphasis enlarges every
recognized critical kind, including procs, summons and healing when the server
explicitly marks it critical. Crits get a bright pulse/starburst/ring; heals use
spiraling green-gold motes and a halo. Turn Subdued effects off to see this art.

Incoming landed vulns use short names: LIGHT VULN (purple), FROST VULN (blue),
ACID VULN (green), FIRE VULN (orange), NETHER VULN (violet), physical vuln labels,
and IMPERIL (warm amber). Colors are editable. These are event notifications,
not a second debuff-icon/duration tracker. Expirations, resisted/surpassed casts
and quoted player chat do not create landed-effect notifications. Refreshes may
appear and count as an application notice. Effects are not invented damage hits.

Explicit spell evidence is classified before physical-looking body-hit text.
Unknown incoming delivery remains in total damage taken but is excluded from
physical and spell defense denominators. Physical still combines melee/missile;
weapon attacks with elemental damage are not automatically assumed to be spells.
Incoming generic spell resists cannot always identify whether a damage or debuff
spell was resisted, so rates describe the supported observed outcomes. Ambiguous
incoming damage has its own detail counter rather than biasing either rate.

Old saved run aggregates cannot be re-parsed because ordinary chat is not retained.
They are preserved and labeled as earlier-parser totals. Use New session for a
clean session sample; the next new Thread starts a clean run under the new parser.

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
and rings; crits burst with rays. Identical same-kind hits within 0.6 seconds coalesce visually as `380 ×3`,
including across targets. Their individual value is not replaced by a summed
number; different amounts, directions, delivery types and critical status stay
separate. Actual damage totals retain every hit. The event queue is bounded to keep busy
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
