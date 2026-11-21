# Marae Lassel v1.11 — advance past unsuccessful bound caches

Fixes CACHE_SEARCH repeatedly reopening one bound cache while other caches are visible.
Before any pickup is issued, an empty or closed bound cache has a bounded eight-second
wait; after an unsuccessful opening attempt, the run releases it and tries other IDs.
Rejected empty containers cannot immediately rebind the run. All IDs can be revisited
in a later scan round for delayed server contents. A pending move or confirmed pickup
keeps its existing binding and acknowledgment behavior. Success still requires real loot.

1. Stop the mission and close AC clients.
2. Extract this entire ZIP and run UPDATE-MARAE-MISSION.cmd.
3. Restart AC, run `/lua start MaraeLassel`, then `/marae`, and choose Resume run.

The updater backs up and verifies the DLL, meta and panel together. It retains the
same mission API 6 native patch, display preferences and completed-task checkpoints.
The failed live scenario (four visible caches, first ID bound/probing, none open) and
an empty-first-cache scenario are covered by simulations, alongside genuine parser,
MoonSharp, Windows x86 and installer checks. Live server verification remains needed.
