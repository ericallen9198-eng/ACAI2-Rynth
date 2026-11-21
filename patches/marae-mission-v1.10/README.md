# Marae Lassel mission v1.10 — parser compatibility and clear mode controls

This update fixes unsupported unary NOT in task eligibility/cache filters, replaces
unsupported min/max expressions with supported iif clamps, quotes legacy messages,
and supplies a narrowly scoped native cache-currency pickup helper (mission API 6).
The helper only moves a currency item from the currently bound open Unearthed Cache
to the player's inventory. Existing serial inventory acknowledgments remain in place.
Ordinary NPC, portal, combat, corpse recovery and other modules are preserved.

## Install

1. Stop the mission and close all AC clients.
2. Extract the entire ZIP (Downloads is fine).
3. Double-click UPDATE-MARAE-MISSION.cmd. Keep all extracted files together.
4. Start AC and run `/lua start MaraeLassel`, then `/marae`.
5. Select the areas you want. Enable **Use maps** for treasure maps. Leave
   **Hunt only** OFF to talk to hunters and do tasks. It intentionally skips NPCs
   when ON. Each option now has its own visible label beside its checkbox.
6. For this test choose **New run** so the previous fallback state and local timer
   decisions are cleared. Server quests are not abandoned or reset. Later use Resume.

The running panel displays the actual runtime mode and map status. Optional camp
limits and per-character preferences remain. Far mesh travel stays 60–180 yards;
close travel stays 10–60 yards, adjustable live for the next clue.

The installer checks hashes, backs up the DLL/meta/Lua together to
`%LOCALAPPDATA%\ACAI2\Backups\MaraeMission\<timestamp>`, and rolls back failed writes.
It accepts the reviewed November 19 original and prior mission releases, including
v1.9. Unknown DLLs are refused before changes. Core API 97 or later is required.
No RynthCore engine binary, Bridge, loot profile or character progress file is replaced.

Targets:
- C:\Games\RynthSuite\RynthAi\RynthCore.Plugin.RynthAi.dll
- C:\Games\RynthSuite\RynthAi\MetaFiles\Marae Lassel - Embedded.af
- C:\Games\RynthSuite\RynthLua\Scripts\MaraeLassel\index.lua

## Validation

The build audits every generated expression and nested/quoted filter with the actual
Rynth expression parser and function registry, with execution disabled. Separate
in-memory actual-engine probes verify task/timer selection and cache filters.
Genuine AF/nav parsing, MoonSharp controller/memory checks, fake-game simulations,
repository checks and Windows installer tests are also run. No test issues game
commands. Live server behavior still needs verification.
