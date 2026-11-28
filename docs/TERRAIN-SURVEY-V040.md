TerrainSurvey v0.4.0 replaces fixed-path inspection movement with checked runtime paths from the installed stock RynthNav. Stock RynthAi follows the corners and continues to own combat, buffs, ordinary loot and corpse recovery. No Rynth DLL patch is installed.

Download **Terrain-Access-Survey-v0.4.0.zip**, extract the whole ZIP and run **Install.cmd**. Confirm your active MetaFiles and RynthLua Scripts folders. The installer adds a separate **ACAI2.Terrain** companion folder and shows its DLL path. Add that DLL once through **RynthCore → Plugins → Add Plugin**, keeping RynthAi and RynthNav enabled.

For an existing survey, stop the old script before installing, then run:

```text
/lua start TerrainSurvey
/lua autostart TerrainSurvey character
/terrainsurvey resume
```

**Resume keeps your progress.** `/terrainsurvey new` replaces this character's survey results and logs. The installer preserves the Data folder and backs up replaced survey/companion files.

The planner verifies complete mesh paths, intended elevations, raycasts and 15-yard camp portal exclusions. It refuses partial paths and straight fallback. It replans after combat displacement, blocked live segments and stationary snags, with bounded left/right mesh approaches before hub retry or deferral. It never adds a competing movement-key controller. Two temporary nav files per character keep runtime file growth bounded.

The survey still inspects 36 terrain approaches in 275 → 180 → 225 order. It does not use treasure maps or NPC quests. Buffing and native corpse recovery remain supported; ordinary looting preferences are preserved. Logs include mesh requests, accepted routes, failed approaches and durable inspection results, excluding ordinary chat.

Validation: 63 controlled Lua recovery/ownership scenarios, 17 mesh-policy checks, genuine upstream AF/nav parser checks, and genuine native path/raycast checks on official camp mesh tiles. Normal departure steps passed at all three camps; 12 of 15 normal/side cases were accepted and three portal-crossing side cases were refused. Windows publish, actual x86 assembly loading and custom-folder installer checks run before this package is released. **Live game testing is still required; not every rock is necessarily represented in the mesh.**

Credit to **Soot** for the established Marae hub connectors and original navigation work; **Drakkon / Rynth** for the native engine, mesh and Lua APIs; **Samm / ACAI2** for the terrain survey and recovery integration.
