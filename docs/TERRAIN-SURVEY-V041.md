TerrainSurvey v0.4.1 improves the terrain evidence and final return while keeping memory, logging and native action ownership bounded.

- Combat, buffs, looting, native busy actions and mesh planning pause the local-return travel timer.
- At the end of the pass, the survey attempts `/ls` and verifies the live Plateau landing. At most two recall attempts are journaled before dispatch; finishing resumes after reconnect or death. Exhaustion records an unconfirmed return and preserves the survey results.
- Accepted mesh corners are logged once per request as **planned geometry**. Actual position samples and endpoint arrivals have separate evidence labels; planning a path is never presented as proof that Samm walked every segment.
- The logger retains eight bounded rotating slots, aggregates ordinary loot chatter, and checkpoints routine activity once/minute. Progress and recovery journal changes still save immediately. The companion retains only the current bounded plan and two temporary route files per character. No separate tile cache or additional walker is introduced.
- Whole-client managed/private/virtual memory and cumulative CPU time are sampled once/minute for later analysis. Samples are optional; they do not force GC, set memory limits or claim to isolate the survey from other apps.
- Other native RynthNav trips block survey dispatch rather than creating competing movement.

Download **Terrain-Access-Survey-v0.4.1.zip**, extract it, stop TerrainSurvey and its Lua script, then run **Install.cmd**. Confirm the active MetaFiles, Scripts and companion folders. No Rynth engine/plugin DLL is replaced.

For a previously installed companion, reload **ACAI2.Terrain** through RynthCore Plugins or restart that client so it loads the updated DLL. For a first installation, add the companion DLL shown by the installer once through **RynthCore → Plugins → Add Plugin**. Keep RynthAi and RynthNav enabled; other macro controllers must be stopped.

```text
/lua start TerrainSurvey
/lua autostart TerrainSurvey character
```

Use `/terrainsurvey resume` for an unfinished pass. Samm's uploaded overnight pass is already complete and archived, so **use `/terrainsurvey new` for tonight's fresh test**. New replaces that character's current diagnostic ring; the old ZIP remains your archive.

Validation: 82 controlled Lua scenarios, 19 mesh-policy/geometry checks, genuine upstream AF/nav parser checks, and required Windows publish, actual x86 load and custom-path installer checks before release. Total live client memory/CPU and in-game behavior still require a live test. The 4 GB client address-space limit remains relevant regardless of installed RAM.

Credit to **Soot** for the established hub connectors and original navigation work, **Drakkon / Rynth** for native mesh, engine and Lua APIs, and **Samm / ACAI2** for survey, diagnostics and recovery integration.
