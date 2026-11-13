# Keyfer on Rynth — v0.3.1

This release restores the existing Keyfer services through a Rynth client adapter.
It does not ship combat rendering, combat parsing, DPS, floating text, attack power,
height, debuff HUDs or Thread automation. Rynth remains their owner. The old combat
sources and renderer are retained for possible future separate-plugin work; they
are excluded from the published plugin/dependency graph.

## What is restored

- Direct tells, addressed General/Allegiance questions, same-speaker contextual
  follow-ups, quiet controls, speaker timeouts and private routing from the Bridge.
- Existing arena commands and sheet loading, ability-aware funny commentary,
  disclaimer, paced rounds, cancellation, final-result completion receipts and
  General cooldown notice. Match simulation and General enablement remain in the
  existing Bridge; no separate simulator or automatic General opt-in is added.
- Conversational emotes, with the existing public-only rule and cooldown.
- DreamWeave world-event capture/announcements and public leaderboard jobs using
  the fixed `/top` catalog. No client-selected arbitrary command jobs.
- Treasure report outbox, standings and winner announcements. Payouts use the
  original claim/journal mechanism and need the exact owner-configured one-MMD
  template; they default disabled. Never assume a submitted bank command confirms
  payment. An ambiguous attempt is held rather than automatically repeated.
- Verified nearby player names (native player bit 0x8) in sampled perception,
  without guessing object kinds from names or distances from coordinates.

Bridge v0.17 is reused as-is. Existing AI keys, memory, behavior, wiki reference,
character sheets, arena settings and treasure ledger stay with that Bridge and its
existing data directory. Do not create a new empty Bridge data folder for cutover.
No API key goes in the Rynth plugin settings.

## Runtime compatibility

The package requires the **RynthCore CoreCLR engine, API 92 or newer**, with managed
x86 plugin support. The legacy NativeAOT-only loader cannot load this managed DLL.
The old public README's .NET 10 x86 NativeAOT build instructions are not the current
managed loader instructions. Confirm your installed engine/runtime, not just the
launcher version number. A newer SDK build does not change an installed engine.
The pinned real SDK remains unchanged; do not replace it with stubs.

Windows CI builds/tests and publishes the manifest-bearing package. A Linux build
checks source and ABI but cannot embed the Windows manifest and is not a finished
Windows installation. No native Windows/game run is claimed from those tests.

## Install

1. Keep the current Bridge process/config/data. Stop the old Decal Keyfer responder
   before enabling the new one. The Rynth ownership lock prevents duplicate Rynth
   responders on this PC, but the old Decal adapter does not share that lock.
2. Download [the compiled Windows ZIP](https://github.com/ericallen9198-eng/ACAI2-Rynth/releases/download/keyfer-v0.3.1/ACAI2-Rynth-Keyfer-v0.3.1-Windows.zip) from the normal GitHub release. No source rebuild is needed. The same verified package is also available as **ACAI2-Rynth-Keyfer-Windows** in its successful verification Actions run (requires GitHub sign-in). Alternatively build on Windows with the .NET 10 SDK:
   `scripts\Bootstrap-RynthCore.ps1` (GitHub source ZIP only), then
   `scripts\Publish.ps1`. It runs controlled tests and writes `artifacts\keyfer-managed`.
3. Extract the entire package into its own directory, keeping its dependencies
   together. Remove any old ACAI2-Rynth pilot entry from the launcher first; do not load both versions. Do not reuse a stale combat-pilot output directory. Add
   `ACAI2.Rynth.Plugin.dll` to RynthCore's plugin list. This plugin does not need
   RynthAI's macro to run; it depends on RynthCore and the local Bridge.
4. In File Explorer enter `%LOCALAPPDATA%\ACAI2-Rynth`. Create that folder if needed.
   Copy the supplied `keyfer.example.json` there and rename the copy `keyfer.json`.
   It enables only DreamWeave / Keyfer Sutherland. Adjust Bridge URL only if your
   existing Bridge uses a different loopback port. Keep `pilot.json` telemetry off
   unless separately using the observation pilot.
5. Leave `treasurePrizeCommand` empty during the first conversation check. To
   restore the same automatic one-MMD payout afterward, set it to exactly
   `/b t p 250000 {winner}` and reload. The amount/prefix cannot come from AI text.
6. Start the existing Bridge using your normal shortcut, then log in on Keyfer.
   `/acai2r keyfer status` should report **Bridge registered** after a valid world
   identity is found. Other characters do not run a responder.

Only the named new files under LOCALAPPDATA are used for client state. Old
`C:\ACAI2` configuration and state are not edited or silently imported. Quiet/emote
settings can be set with the commands below. The Bridge history remains the old
history when its original settings/data are retained. This is intentionally not a
copy of every old client-side file.

## Local controls

```text
/acai2r status
/acai2r keyfer status
/acai2r keyfer reload
/acai2r keyfer quiet on
/acai2r keyfer quiet off
/acai2r keyfer emotes on
/acai2r keyfer emotes off
/acai2r keyfer treasure
/acai2r keyfer standings
```

`reload` rereads keyfer.json. A just-closed session may briefly retain its ownership
lock while cancellation finishes; the adapter retries starting automatically within five seconds after it closes. A corrupt settings
file is not replaced automatically. `/acai2r ui` reports that combat UI is excluded.
Observation exports still exist, but capture defaults off and excludes tells. A
private question is sent only to the local Keyfer ask route, never public telemetry.
The Bridge's own scoped conversation persistence is unchanged.

## First live checks

- Ask from another character by tell: expect one tell reply, no public echo/emote.
- Ask an addressed Allegiance question; verify normal speech and quiet controls.
- Try a long or repeated General question; expect the existing short tells notice
  and the actual answer privately. Pasted NPC tells in public must stay public
  inputs rather than becoming direct private-message envelopes.
- Test `!arena Toe vs Samm` in Allegiance. Expect for-fun disclaimer, four-second
  paced updates and final result, then another Allegiance match without a General
  cooldown. Leave existing General enablement unchanged until satisfied.
- Logout while an ask is pending: no old answer may appear on the next character.
- Watch one treasure report and standings before configuring automatic payments.

The tests use fake HTTP and native-send delegates, not production Bridge/game/AI
services. They check ask identity, tell isolation, native command restrictions,
arena pacing/completion and cancellation; they do not prove engine chat hooks or
network dispatch inside the Windows game. Rynth remains responsible for botting,
loot, buffs, graphics, movement and inventory actions outside these fixed Keyfer
chat/leaderboard/prize commands.

## Updating from v0.3.0

Close the game, replace the package files together from the v0.3.1 ZIP, and relaunch
Keyfer. Preserve your existing `%LOCALAPPDATA%\ACAI2-Rynth\keyfer.json` and Bridge.
The regression fix removes System.Net.Http.Json helper calls: Rynth loads
System.Text.Json privately per plugin but shares the HTTP JSON extension assembly,
so passing private JsonTypeInfo values to those extensions fails method resolution.
Serialization now stays inside the private context; ordinary HTTP sees JSON text.
A simulated engine assembly-loader test reproduces the original failure and checks
registration plus private tell delivery after the fix. A missing-method failure now
includes the method signature in local status rather than just its exception type.
