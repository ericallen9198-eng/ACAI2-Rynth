# Marae Lassel mission v1.2 memory fix

For the already installed Marae mission v1 compatibility DLL. This package updates
only the embedded AF and Lua panel. It does not replace RynthAi, the engine,
Bridge, loot profiles, or per-character settings/checkpoints.

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
- C:\Games\RynthSuite\RynthAi\MetaFiles\Marae Lassel - Embedded.af
- C:\Games\RynthSuite\RynthLua\Scripts\MaraeLassel\index.lua

The updater checks source hashes and the exact released v1 compatibility DLL,
backs up both target files under
`%LOCALAPPDATA%\ACAI2\Backups\MaraeMissionUpdate\<timestamp>`, and rolls back both
if a write fails. restore-info.json records original paths. To undo, close AC and
copy each listed Backup back to its Target. No DLL is included in this update.

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

Concurrent Unearthed Caches (template 1004150) are tried by distinct object ID,
within 80 native distance units of the dig anchor. Already present caches from
before the dig and objects with unreadable coordinates are excluded. Each
candidate gets eight seconds to open, then the next is tried. An actual open
container acknowledgment binds the reward; subsequent currency moves must receive
inventory acknowledgment. No ownership is inferred from display name or description.
Doubloons and optional notes are picked up; an actual direct/corpse doubloon payout
can finish without a chest. Exhausting candidates refreshes the list until the
existing five-minute collection deadline. A timed-out or unacknowledged inventory
move still holds for review rather than falsely claiming a reward.

Embedded routes, /ls hub travel, per-character reconnect checkpoints, and native
corpse-recovery coordination remain. Native recovery needs the existing RynthNav
support and a recoverable corpse; no login-screen automation is added.

## Verification and limits

Genuine Rynth AF/nav parser and MoonSharp checks, fake-interface mission simulations
(including your live Northern hunter replies and multiple caches), repository
regression checks, and Windows PowerShell 5.1 updater checks validate this build.
Native use actions are not issued by tests. Live server behavior still needs testing.
Source: https://github.com/ericallen9198-eng/ACAI2-Rynth
