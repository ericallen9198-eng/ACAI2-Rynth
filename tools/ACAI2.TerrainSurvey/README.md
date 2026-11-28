Terrain Survey v0.3 generates a stock AF from the preserved mesh-planned routes in `data`. These JSON files are planned geometry, not runtime/player exports. Rebuild from the repository root:

```sh
python tools/ACAI2.TerrainSurvey/build_survey.py --data tools/ACAI2.TerrainSurvey/data --out metas/terrain-survey
```

The Lua recorder uses genuine stock filesystem/JSON/RynthAi APIs. It streams bounded rotating JSONL, atomically checkpoints results before AF acknowledgment, and preserves per-character progress. The AF watchdog can restart a missing recorder; write failures retry independently. Native RynthAi remains the corpse/combat/loot owner. Recorded ordinary player chat is excluded.

Controlled tests use genuine MoonSharp with fake game, clock and filesystem interfaces. They do not prove live game behavior. `scripts/test.sh` and `scripts/Test.ps1` include these scenarios. The AF was also checked using the unmodified upstream AF/nav parsers at RynthSuite commit `4602d9267d0892da7179cac9f70ea5622e3a1b7e`. The 2,125 checked mesh raycasts have no blocked segments; some nearest-poly height ambiguity remains, so actual probe arrival includes a height check. No Rynth DLL is patched or bundled.

The playable assets and file-only Windows installer are under `metas/terrain-survey`. Linux checks cannot prove Windows installation or an in-game run. Runtime checkpoints, logs and binary mesh tiles stay out of Git.

The v0.3 fix moves repeated movement monitoring out of one-shot AF rules and into Lua ticks, includes combat/buff/loot settling, guards portal arrival, and rechecks old false no-progress results. Seven hub approaches were replanned using genuine DotRecast and upstream RynthNav PortalAvoid/AvoidFilter around a 12-yard staging exclusion. PortalClearance requires real upstream sources (RynthSuiteRoot) and verified official hub mesh tiles; those tiles are not committed.
