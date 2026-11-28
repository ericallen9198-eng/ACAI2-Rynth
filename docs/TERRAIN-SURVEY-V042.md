TerrainSurvey v0.4.2 fixes the `WAIT_275_01; mesh idle; wait combat` starvation seen in dense, quickly respawning packs.

Walking inspection routes alternate **15 seconds of actual movement** using native Boost Nav Priority with **up to 30 seconds of normal fighting and looting**. The fight/loot window ends early after at least three seconds when targets and nearby loot work clear. New combat candidates do not reset the window or prevent the next travel burst. A blocked attempt with nearby targets also releases navigation for a bounded fight window before retrying.

RynthAi remains the sole walker, fighter, healer, buffer and looter. Native healing and buffs retain priority. Ordinary looting follows the player's setting and profile; these windows cannot guarantee every corpse is looted. The original Boost Nav Priority setting is checkpointed before changes and restored on Stop. Runtime mesh checks, portal clearance, native corpse recovery, bounded logging and the final verified hub-return attempt are retained.

To update your unfinished pass:

1. In game: `/terrainsurvey stop`, then `/lua stop TerrainSurvey`, then `/ra stop`.
2. Download **Terrain-Access-Survey-v0.4.2.zip**, extract the whole ZIP, and run **Install.cmd**. Confirm your actual installation folders.
3. Reload **ACAI2.Terrain** in RynthCore Plugins, or restart the client, to load the updated companion. Keep RynthAi and RynthNav enabled.
4. Run `/lua start TerrainSurvey`, then `/terrainsurvey resume`.

Use **Resume** to retain this unfinished pass. Status now includes the travel/fight_loot cycle. Mesh idle during fight_loot is expected. No Rynth engine DLLs, loot profiles or saved survey Data are replaced.

Validation: 86 controlled Lua scenarios using genuine MoonSharp; 19 mesh-policy checks; required repository checks; genuine upstream AF/nav parsing; Windows publish, actual x86 DLL-load and repeated custom-path installer checks. These checks do not replace live game testing. Memory/logging bounds are unchanged; the client still has a 4 GB address-space limit.
