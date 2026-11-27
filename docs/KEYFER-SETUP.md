# Keyfer on Rynth — plugin v0.3.5 / Bridge v0.20

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

The current source includes Bridge v0.20, which defaults to GPT-6 Luna and preserves the v0.19 moderation behavior.
For the Bridge-only model update, see [KEYFER-LUNA-UPDATE.md](KEYFER-LUNA-UPDATE.md).
The older v0.3.5 full package linked below still includes Bridge v0.19; apply the Bridge-only update afterward. Stop the old
Bridge before launching the included Bridge\ACAI2.Bridge.exe. Existing AI keys, memory, behavior, wiki reference,
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

1. Close the game and stop the current Bridge process. Keep its configuration/data. Stop the old Decal Keyfer responder
   before enabling the new one. The Rynth ownership lock prevents duplicate Rynth
   responders on this PC, but the old Decal adapter does not share that lock.
2. Download [the compiled Windows ZIP](https://github.com/ericallen9198-eng/ACAI2-Rynth/releases/download/keyfer-v0.3.5/ACAI2-Rynth-Keyfer-v0.3.5-Windows.zip) from the normal GitHub release. No source rebuild is needed. The same verified package is also available as **ACAI2-Rynth-Keyfer-Windows** in its successful verification Actions run (requires GitHub sign-in). Alternatively build on Windows with the .NET 10 SDK:
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
6. Start the included Bridge\ACAI2.Bridge.exe (console must show v0.19), then log in on Keyfer. Your shortcut may still be used if it points at this updated copy.
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

Close the game, replace the package files together from the current ZIP, and relaunch
Keyfer. Preserve your existing `%LOCALAPPDATA%\ACAI2-Rynth\keyfer.json` and Bridge.
The regression fix removes System.Net.Http.Json helper calls: Rynth loads
System.Text.Json privately per plugin but shares the HTTP JSON extension assembly,
so passing private JsonTypeInfo values to those extensions fails method resolution.
Serialization now stays inside the private context; ordinary HTTP sees JSON text.
A simulated engine assembly-loader test reproduces the original failure and checks
registration plus private tell delivery after the fix. A missing-method failure now
includes the method signature in local status rather than just its exception type.

## v0.3.2: safer conversation and occasional player reactions

Toe and Samm receive addressed answers and contextual follow-ups in the channel
where they ask, including local chat. The adapter suppresses the Bridge's generic
public-to-tells redirect for those two public requests. Their private tells remain
private. Public owner dialogue uses an audience-scoped conversation key so a
legacy Bridge handoff cannot load the player's real private-tell history and then
broadcast it. Local dialogue uses the Bridge's supported public Allegiance context
for generation while native delivery remains local; it is not cross-posted as an
Allegiance observation. Local arena commands use the existing private-preview route.
General mute still applies to other players; public replies must remain
enabled for public questions. Local chat from other players is eligible for rare
ambient reactions, but is not forwarded to the Bridge or automatically answered.

Command-execution bait, instruction/secret extraction, and explicit abusive/sexual
bait addressed to Keyfer are checked before Bridge requests. Detected offenders
receive a private notice and an interaction timeout: 5 minutes, then 30 minutes,
then 24 hours for repeat strikes within 30 days. This blocks Keyfer interactions,
not the player's game chat. Ordinary command-help questions and identity labels
are not offenses. Existing Bridge moderation remains active. These deterministic
checks supplement, rather than replace, the fixed native command catalog; no
keyword filter can recognize every possible adversarial conversation.

Bounded timeout deadlines and strike counts persist in the character's existing
KeyferState file; raw offending messages are not saved. Public/tell speech replaces
recognized bait names with a neutral challenger label without changing real tell
recipients, sheet names or arena lookups. Final speech is screened again. A name
alone does not earn a timeout, and identity labels such as Non-Binary are not bait.

With `keyferAmbientReactions` true (the default), Keyfer occasionally laughs,
applauds, sighs or shrugs at eligible nearby/allegiance chat. Sometimes he adds a
short contextual remark in that same channel. He never cross-posts private tells,
reacts to spell incantations, quotes player text into emotes, or claims unobserved
combat. There is a one-minute login settling period, at least five minutes between
spontaneous reactions, and the shared 90-second emote cooldown. Quiet mode,
`emotes off`, disabled public replies, pending replies and active arena requests
suppress ambient reactions. General has no spontaneous chatter. The nine fixed
existing text emotes are reused; there are no arbitrary emote commands or animations.

To disable spontaneous reactions alone, set `keyferAmbientReactions` to false in
your existing keyfer.json and use `/acai2r keyfer reload`. Existing quiet/emote
controls continue to work. Those v0.3.2 features alone needed no Bridge update;
v0.3.3 loot awareness below requires the included Bridge v0.18. API keys stay unchanged.

## v0.3.3: Keyfer knows his own chest pulls

UPDATE BOTH PARTS. Close the game and old Bridge. Extract the entire new ZIP into
your existing Keyfer plugin folder, replacing package files together. Keep your
existing keyfer.json, C:\ACAI2\Data, behavior files, configuration and environment
variables/API key. Run the included Bridge\ACAI2.Bridge.exe instead of the old
Bridge, then launch the game. Bridge v0.18 is self-contained Windows x64, so no
separate .NET install is needed for it. The plugin remains managed x86/API 92+.
If your existing shortcut sets environment variables, keep those settings and point
its executable at the new Bridge. Two Bridges cannot share port 5052.

Chest awareness is passive. RynthAi still owns the chest/key use and loot profile;
ACAI2 issues no chest, inventory, ID, salvage, bank or vendor actions. On Keyfer's
client only, it reads RynthAi's Item Tool status via its real read-only export and
recognizes the actual `[RynthAi] Looted [Keep] ...` confirmation. Rule previews and
`Looted [Salvage]` / `A mule cannot salvage` messages do not count as Keep findings.
A completed chest cycle is not an item count, and 'nothing matches' is not proof
that the chest was empty. Starting observation mid-batch preserves RynthAi's
reported count without claiming the plugin witnessed those earlier cycles.

Native chest contents followed by the same item becoming owned by Keyfer can
confirm an item's chest source. Missing/ambiguous correlations remain unconfirmed,
not guessed. This never requests an identification. A missing RynthAi export or
native sample must not stop normal conversation. New typed loot context reaches
the actual user ask; it is not pasted into player chat or injected into the user's
question. Private tells remain restricted to their original conversation route.

Displayed attack/melee-defense percentages remain separate from estimates with
known item cantrips included. Example: +16% attack plus Legendary Heart Thirst
estimates 25%; +18% melee defense plus Epic Defender estimates 25%. The kept rule
name is retained so Keyfer can describe what actually qualified it.

Try: `Keyfer, what have you kept from those chests?` or `Keyfer, still pulling?`
He can answer naturally from current evidence; this does not call AI for every
cycle or announce every pickup. In-game wording remains model-generated.

Only 12 recent Keep records and runtime counts are held in memory. The last 24
notable MOD25 / estimated >=25% attack-or-defense findings are stored in one
`notable-loot.json`, capped at 48 KiB, under:
`%LOCALAPPDATA%\ACAI2-Rynth\KeyferLoot\<server>\<character>\`
Only item name, rule, base/estimated modifiers, a short cantrip list, timestamp and
source confirmation are saved. No chest transcript, raw loot lines, salvage junk,
private chat or per-cycle files. Context uses at most 30 days of dated findings;
new login resets current-session counts. History is not proof of present inventory.
The Bridge receives findings only with a question, and creates no second loot log.

The new package does not alter your loot profile. Select the corrected v2 mule
UTL separately to stop RynthAi's old salvage rules. Combat and Threads are unchanged.

## v0.3.4: Keyfer chest setup and owner chat controls

Update the Keyfer plugin and its accompanying DLLs together while AC is closed.
Keep your existing keyfer.json and secrets. This change needs no Bridge update.
Copy `Lua/KeyferChest` from this ZIP into
`C:\Games\RynthSuite\RynthLua\Scripts\KeyferChest` (index.lua inside that folder).
Then run `/lua start KeyferChest`. `/keyferchest` shows its small setup panel.

On the configured Keyfer client: select a key, click **Set selected key**, select
a chest, click **Set selected chest**, and check the native confirmations in chat.
The panel's names show requested selections, not a fabricated native acknowledgment.
Use **Show native status in chat** to verify setup. Load the no-salvage mule profile.
Chest/key binding remains owned by Rynth's Item Tool; the script does not run a
second looter or automatically restore a selected world object after relogging.

Only Toe and Samm can send these exact phrases in tells or allegiance:
- `Keyfer pull keys`
- `Keyfer stop pulling`
- `Keyfer pull status`

They bypass AI interpretation and dispatch only fixed start/stop commands. The
actual sender and outer chat envelope are checked; General/local chat and quoted
or extended instructions cannot trigger them. Starts require a fresh native
snapshot with a configured chest and key; duplicate starts settle before retry.
Requests expire after five seconds and cannot replay across sessions. Replies use
the requesting channel. Start acknowledgments mean requested, not proof of success;
ask for pull status to read Rynth's result. Native key shortages and reset waits
remain Rynth-owned. The adapter needs its usual active Bridge registration, but
these commands never ask the AI. The local setup panel does not need a model call.

Optional: `/lua autostart KeyferChest character` enables this panel for Keyfer on future logins.

## v0.3.5: direct insults and original-channel boundaries

Close AC and the old Bridge. Extract package files together into your existing
Keyfer folder, replacing matching files. Update the Bridge subfolder too. Keep
keyfer.json, API keys, environment variables, behavior files and the original Data
folder; do not replace configuration with the example. Start the updated
Start-Keyfer-Bridge.cmd (Bridge v0.19), then launch AC. No source build is required.

Direct insults and derogatory renaming now get an immediate 24-hour reply timeout
and one curated first-person notice in the channel where they happened. This is
Keyfer ignoring the player, not a server or Discord moderator action. In-game
Samm and Toe are exempt only from this new insult rule. Ordinary command/secret
protections remain. Repeated insults neither extend the deadline nor repeat the
public notice. Normal questions, quoted/reported speech and identity labels are
not classified as insults. The matcher is deliberately conservative; it does not
claim to understand every possible insult. Notices work without an AI call.

See KEYFER-BEHAVIOR.md for the full current behavior guide and preserved settings.


## v0.3.5: same-channel direct-insult timeout

Update **both parts** this time: close the game, stop the old Bridge, then replace
all package files together in your existing Keyfer folder. Preserve your existing
keyfer.json, API key/environment settings, behavior files and Data directory.
Start the included Start-Keyfer-Bridge.cmd (or a shortcut to it); its console
should say **ACAI2 Bridge v0.19**. Then launch the game. No source build is needed.

Recognized direct insults from anyone except in-game Samm/Toe trigger a persistent
24-hour reply timeout and one first-person notice in the original channel.
Repeated insults do not renew the deadline or repeat public notices. This is
Keyfer ignoring messages, not server moderation. Existing command protections,
chest controls and loot awareness remain. Read **KEYFER-BEHAVIOR.md** in the ZIP
for the existing and new behavior parameters, exceptions and limitations.
