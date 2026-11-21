# Marae Lassel mission v1.6 per-area hunt-only and optional mesh map steps

Includes a new x86 mission compatibility DLL (mission API 3), the embedded AF,
and the low-allocation Lua controller. All three must be updated together. The
installer accepts the reviewed November 19 original/NPC patch, the prior released
mission DLL, or the exact new bundled DLL. Unknown RynthAi builds are refused before
any changes. RynthCore engine binaries, Bridge, loot profiles and character progress
are preserved.

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

## Continuous hunting per area

Each selected area has independent **Maps** and **Hunt only** checkboxes. Hunt only
skips NPC pickup/turn-in and quest completion/cooldown/progress gates, while combat,
buffing and your current loot profile remain active. Maps OFF never uses a map,
even if one is in your pack or every quest has been completed. No loot-profile,
salvage-rule or server salvage setting is changed by this update.

For overnight Lowlands hunting: select **180 Lowlands** only, enable that row's
**Hunt only**, and leave **Maps** unchecked. Press Start/Resume. No New run/reset
is required to bypass previously completed task flags.

A single hunt-only area repeats locally after its nav ends. Multiple hunt-only
areas rotate in 180/225/275 order after each completed circuit; selected task areas
run first. Each area's map toggle is respected. 180/225 with maps OFF use copies
of the supplied Hunt Loop geometry with an end marker; 275 uses its supplied
area circuit. All 20 navs are embedded. Character choices and the continuous-area
cursor persist through reconnect. Death recovery returns to the chosen hunting
mode without an NPC check. Completed task flags are not used as hunt-only stop gates.

## Optional mesh navigation during treasure mapping

**Mesh map steps (needs RynthNav)** is separate and OFF by default. It only changes
map-clue movement; it does not change ordinary hunt-route movement or enable maps.
Enable it when testing treasure hunting with a RynthNav plugin exposing Path v2
and navmesh coverage for this area.

Each directional clue becomes a short destination: up to 24 native distance units
for a far clue, 8 for a close clue. The new native mission API requests a complete
path from RynthNav with portal avoidance and wall clearance, installs its corner
waypoints as a temporary RynthAi route, and re-reads the map after actual arrival.
RynthAi remains the movement owner; a separate RynthNav travel controller is not
started. The original hunt route is preserved by the existing native route snapshot.
Health loss yields to normal combat/recovery; Stop/death/return release the walk.

Unavailable mesh/no path retries shorter steps twice, then pauses with an explicit
message. A walk has a 45-second timeout. It never silently falls back to blind
forward/backward setmotion while the mesh option is enabled. Turn the option OFF
to retain the original clue movement if mesh coverage is unavailable. Mesh baking
can miss physical obstacles, so live testing remains necessary; collision-free
travel is not guaranteed.

Tests cover completed/cooldown quests, hours without KT progress, maps OFF with maps
in inventory, single-area repeating, multi-area rotation, corpse recovery, per-character
preferences, map-clue directions, mesh arrival versus dispatch, failed-path retries,
health interruption and the Lua allocation budget. Native pathfinding itself needs
a live RynthNav client to verify.

## Digsite return and cache approach

The first successful dig records the player's position; an encounter entered without
a first-dig observation uses its starting position. Combat may still chase enemies.
After a cache-producing completion, the mission disables combat/nav, returns to
within three native distance units of the recorded digsite when needed, then searches.
It does not require multiple caches: one named cache is a sufficient candidate.

The selected cache ID gets an explicit approach phase when its readable position is
more than 0.9 distance units away. Lua submits a one-point route through genuine
`rynth.ai.GoTo`; the meta waits for measured arrival before issuing use on that ID.
Returning/approaching has a 45-second timeout and shares the five-minute collection
deadline. A failed approach rotates to another ID; denied opens still rotate after
eight seconds. Missing cache coordinates do not exclude a candidate: direct use is
still attempted. Actual open-container and serial inventory acknowledgments remain
required. Existing Aun corpse looting and direct doubloon payouts remain supported.

The native API snapshots the original hunt route object, path and waypoint index
before temporary walking and marks the specific temporary route as owned. Completion,
Stop, death or error releases that snapshot. Temporary walks use a tight runtime-only arrival tolerance, restored with the route.
Restore never starts a macro or enables
navigation. It does not replace a route changed by another owner and does not restore
a route to another character. Reconnect still uses saved mission progress; a partially
issued currency move is never replayed automatically.

Status text now distinguishes returning to the digsite, walking to the selected
cache, waiting for it to open, collecting currency, and refreshing cache IDs.
The prior message did not imply multiple caches were required.

Offline tests include a single cache 24 yards away, actual arrival versus movement
dispatch, post-chase return, unreachable-candidate rotation, death during approach,
native-recovery exclusion, one GoTo per request, Stop restoration, and the memory
allocation budget. They do not prove live server pathfinding or collision handling.

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
