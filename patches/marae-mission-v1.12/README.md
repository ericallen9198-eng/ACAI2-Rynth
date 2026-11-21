# Marae Lassel mission v1.12 — NPC approach correction

Marked hunter waypoints now resolve the nearby exact-name NPC before checking the
recorded waypoint coordinate. A visible NPC is used through AC's native interaction
and approach behavior. Coordinates only guide travel when the target is not visible.
This removes the requirement to push into the recorded standing spot before talking.

The existing named-reply confirmation, eight-second retry gate, ordinary NPC/portal
handlers and all v1.11 multi-cache fixes remain. No Follow/Nav distance preference
is changed. The native mission interface remains API 6; this is a handler correction.

## Install

1. Stop the mission and close all AC clients.
2. Extract the entire ZIP anywhere, including Downloads.
3. Run UPDATE-MARAE-MISSION.cmd with all extracted files kept together.
4. Start AC, type `/lua start MaraeLassel`, then `/marae`, and choose Resume run.

The updater backs up the active DLL, meta and panel before installing and verifying
them together. Saved character settings and task progress are preserved. A failed
write restores the previous files. Backup paths appear in the installation window.
The bundled DLL requires Core API 97 or later and uses the reviewed November 19 base.

## Validation

Windows x86 compilation/manifest checks, genuine NPC reply-gate and nav parser tests,
mission simulations including stale-bound multiple caches, actual expression-engine
audit, MoonSharp controller/memory checks and installer tests run before publication.
Tests issue no live game actions. Live NPC/pathfinding behavior still needs verification.
