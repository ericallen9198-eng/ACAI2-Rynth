# Marae Lassel mission — embedded routes and resume

Close all AC clients, extract this ZIP in Downloads, and double-click **INSTALL-MARAE-MISSION.cmd**. No path entry is needed. It installs all three files, after verifying checksums and backing up every existing target:

- `C:\Games\RynthSuite\RynthAi\RynthCore.Plugin.RynthAi.dll`
- `C:\Games\RynthSuite\RynthAi\MetaFiles\Marae Lassel - Embedded.af`
- `C:\Games\RynthSuite\RynthLua\Scripts\MaraeLassel\index.lua`

It accepts only the reviewed original 2026.11.19.1 x86 DLL or our matching NPC v1 patch. Other DLLs are refused. No engine, Bridge, other ACAI2 plugin, loot profile, or private settings/history file is installed. Backups and restore metadata are in `%LOCALAPPDATA%\ACAI2\Backups\MaraeMission`. Reinstalling identical files does nothing.

## Start

Start AC, type `/lua start MaraeLassel`, and open **Marae Lassel** (`/marae` toggles it). Select areas and each area's **Use maps** checkbox. Press **Start run**. The button stops the previous RynthAi macro, explicitly loads **Marae Lassel - Embedded.af**, verifies its identity, restores that character's checkpoint, resets Default, enables meta processing, and starts. It shows loading errors and timeouts instead of running the wrong meta. No manual meta selection or separate nav files are required: **18 routes and all 27 hunter confirmations are embedded**.

The run visits selected areas in order **180 → 225 → 275**. At each camp it talks to the hunters, waits for their replies, hunts their assigned creatures, uses treasure maps when selected, verifies turn-ins, and advances to the next selected area. It does not wait for daily quest timers. If the server says a task is unavailable/already rewarded, that is handled as an observed server response rather than a guessed timer. The original standalone Maps-only mode is omitted from this simpler task-plus-maps panel.

## Travel

This character's `/ls` is bound to the **275 Plateau camp**, used as the travel hub. If already at the selected camp, the mission does not recall unnecessarily. Otherwise it uses `/ls`, verifies the Plateau landing, and takes the appropriate exact-name portal:

- `Southern Hamlet Hunters (Rec Lvl 180)` → arrival `0x14B5002F`.
- `Northern Hunters' Camp (Rec Lvl 225)` → arrival `0x1BC2002B`.
- 275 uses the hub directly, in landblock `0x21B7`.

Both portal approaches use the player's supplied position `0x21B70027 [113.813011 156.236633 92.934402]`. Arrival checks use the supplied camp landblocks, allowing normal step/pivot changes within camp. Native portal readiness and positive HP suffice; no maximum/full-HP equality or optional `isportaling[]` expression gate is used. If `/ls` lands elsewhere, the run holds with an error instead of blindly walking an unrelated route. `/ls` must work from the current server location; a server restriction/refusal cannot be bypassed by the meta.

## Disconnect, death and corpse recovery

Run intent, completed areas and 27 task flags are stored per character in RynthLua's compact character storage; no transcript or unbounded history is saved. The panel registers itself for character autostart on Start. On a real reconnect it reloads the mission, restores flags, travels to/reverifies the interrupted area's hunters, and continues unfinished tasks. It does not blindly replay an interrupted map press, item move, or old nav index. Explicit Stop prevents automatic restart; Resume retains progress, and New run clears the mission checkpoint. Auto-resume does not take over a different running macro. A client still needs to be logged back in; this does not automate credentials or the login screen.

The mission applies **Recover My Corpse ON**, **Travel Back To My Corpse ON**, **Stop Macro On Death OFF**, native Recall On Death OFF, and standalone Keep Hunting After Death OFF. In this UI, Safety Stops is under **General → Safety Stops**. There is no second-death limit. Rynth owns corpse movement/looting; the meta waits on a read-only native recovery-busy query before recalling or loading a travel route.

**RynthNav must be loaded for corpse travel.** Native automatic corpse travel is for outdoor deaths and has its own reachability/time limits; nearby own-corpse looting also works where available. Rynth's death-spot memory is session-local, so a disconnect before recovery can prevent automatic travel back; opportunistic recovery remains enabled when the corpse is encountered. Indoor/unreachable/decayed corpses are not a promise of automatic retrieval. Once native recovery finishes or gives up, the mission resumes the interrupted area. It never requires full health or an unchanged buffed HP maximum.

## Treasure collection

The acknowledged currency collector from the verified treasure meta is retained: explicitly moves Doubloons and optional trade notes from the bound open cache, waits for inventory/source acknowledgment, and never counts a timeout as success. A corpse payout can complete on the actual Doubloon increase without waiting for a nonexistent chest or notes. Unconfirmed sent moves hold for review rather than blind retry. Existing loot profiles remain installed and selected by the player.

## Compatibility and limits

Built from pinned genuine RynthSuite `1e7c9924c816d8b2c29f27eda95b8ab7e5239870` and RynthCore `fc246c0a4277fbc68207302e6d7ce22a16715fa6`, **2026.11.19.1 managed win-x86, Core API 97+**. Keeps marked-NPC support and adds only read-only `maraemissionapi[]`/`maraerecoverybusy[]` expression hooks for this controller. No new combat controller or engine ABI is added.

This is a testing build. Genuine parsers, a genuine MoonSharp interpreter with fake game interfaces, offline rule simulations and a Windows build check code/structure; they cannot establish live pathing, dialogue, quest completion, server restrictions, or HUD behavior. Confirm one hunter and one camp transfer in-game before leaving a long run unattended. Report the panel's loaded meta, state, status and nearby chat if it holds.

## Restore

Close AC. `restore-info.json` in the printed backup directory lists each target and its old backup. Restore entries whose `Existed` is true; remove only the newly installed mission file for entries that previously did not exist. The earlier original NPC-patch backup is also left intact.
