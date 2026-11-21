# Marae Lassel mission v1.4 with DLL patch restoration

Includes the same tested x86 RynthAi mission compatibility DLL from mission v1,
plus the latest cache-discovery and Lua-memory fixes. The updater verifies the
installed DLL and reinstalls the patched DLL when it differs from the bundled
copy and is one of the reviewed original/NPC-patch builds. If it already matches,
its bytes remain the same. An unknown/newer RynthAi DLL is refused before any file
is changed; send that DLL for version matching rather than downgrade it blindly.
RynthCore itself, Bridge, loot profiles and character checkpoints are preserved.

1. Stop the mission in its panel, then close all AC clients.
2. Extract the entire ZIP anywhere (Downloads is fine).
3. Double-click UPDATE-MARAE-MISSION.cmd. Keep both scripts, checksums.json and
   the Mission folder together. The window stays open with the result.
4. Start AC, type `/lua start MaraeLassel`, then `/marae` to show the panel.
5. Choose **Resume run** to retain completed tasks. Use Start run if no saved run.
   The default enabled **Fallback to 180 treasure hunting** continues map hunting
   after selected quest work is exhausted or unavailable. Turn it off to finish
   normally instead.

Installed targets:
- C:\Games\RynthSuite\RynthAi\RynthCore.Plugin.RynthAi.dll
- C:\Games\RynthSuite\RynthAi\MetaFiles\Marae Lassel - Embedded.af
- C:\Games\RynthSuite\RynthLua\Scripts\MaraeLassel\index.lua

The updater validates all three source hashes, backs up every existing target
under `%LOCALAPPDATA%\ACAI2\Backups\MaraeMission\<timestamp>`, installs and verifies
the DLL/meta/panel together, and restores the old files if a write fails.
`restore-info.json` records original paths. To undo, close AC and copy each listed
Backup back to its Target. An identical reinstall changes nothing and retains the
original backups. No RynthCore engine binaries are replaced.

Updating RynthCore does not prove that RynthAi was replaced. This package checks
the actual DLL at your supplied active path. The bundled patched DLL requires
Core API 97 or later; newer engine compatibility still needs live verification.

## Memory fix

The controller now uses RynthLua's allocation-light monotonic clock to skip repeated
game ticks before polling. Normal state/display polling runs once per second;
startup runs at most five times per second, while button requests bypass the gate.
Progress is checkpointed every five seconds and immediately on Stop/finish.
The same display snapshot is reused; HUD refresh is five times per second.
Ordinary combat chat is rejected before expression reads. No memory-limit changes
or forced garbage collection are used. Existing mission_v1 character storage remains
compatible, including auto-resume and learned hunter targets.

A genuine MoonSharp allocation regression test covers 200 ticks/second, 100 ordinary
combat messages/second and five HUD renders/second for five simulated seconds.
Its 16 MiB ceiling covers the Lua controller with fake native interfaces, not the
complete live game's memory use. Live confirmation of the memory guard is still needed.

## Existing behavior preserved

Hunter replies use the exact NPC identity and explicit in-progress, complete,
reward or cooldown status, without requiring an exact kill total or target label.
The controller learns targets from NPC replies to attribute server task-complete
notices. Intro and abandon instructions alone are not confirmation. Missing
replies receive one route retry (at most 180 seconds per pass), then the mission
hunts confirmed unfinished tasks. Unresolved tasks remain unresolved; other areas
can proceed. With no confirmed task work remaining, the default is 180 map hunting.
A new run resets deferred-area checkpoints; Resume preserves them.

NPC mismatches retain only the latest 16 entries per character, at most four
512-character replies per entry; identical entries merge a repeat count.
No ordinary player chat transcript is saved. The diagnostic JSON is exported to:
`C:\Games\RynthSuite\RynthLua\Data\MaraeLassel\npc-mismatches-<server>-<character>.json`
(default RynthLua data location). Export failure does not stop hunting.

Unearthed Cache discovery returns to exact-name landscape enumeration, matching
`Unearthed Cache`. Coordinate availability, distance-to-dig-anchor, template metadata
and a pre-dig exclusion list no longer block a candidate. Each distinct ID is tried
once per round, allowing eight seconds for an actual open-container acknowledgment.
Denied caches rotate to the next ID. The opened cache ID remains bound through serial
currency moves and inventory acknowledgments. Reopening uses that exact ID. No
currency pickup is credited from action dispatch alone. Notes remain optional and
actual direct/corpse doubloon payouts do not require a cache. The existing collection
deadline and uncertain-inventory-action hold remain.

The user's live check established that the cache appeared in template enumeration,
was absent from the exclusion/tried lists, and opened when used by exact object ID.
The old combined discovery filter did not select it; its precise failing predicate
was not established. This update removes those added metadata prerequisites rather
than claiming a confirmed ownership or engine diagnosis. Live cache pickup must be
verified after installation. The prior Lua memory fix remains included.

Embedded routes, /ls hub travel, per-character reconnect checkpoints, and native
corpse-recovery coordination remain. Native recovery needs the existing RynthNav
support and a recoverable corpse; no login-screen automation is added.

## Verification and limits

Genuine Rynth AF/nav parser and MoonSharp checks, fake-interface mission simulations
(including your live Northern hunter replies and multiple caches), repository
regression checks, and Windows PowerShell 5.1 updater checks validate this build.
Native use actions are not issued by tests. Live server behavior still needs testing.
Source: https://github.com/ericallen9198-eng/ACAI2-Rynth
